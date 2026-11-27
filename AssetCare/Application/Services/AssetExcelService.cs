using System.Globalization;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class AssetExcelService(
    AssetService assetService,
    IRepository<Asset> assets,
    IRepository<AssetCategory> categories,
    IRepository<Location> locations,
    IRepository<Department> departments,
    IRepository<User> users,
    IRepository<Vendor> vendors,
    IRepository<AppSetting> settings,
    ISpreadsheetService spreadsheet,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IAssetExcelService
{
    public const int MaxImportRows = 500;

    private const string AssetNo = "資產編號", Name = "資產名稱", Category = "類別代碼", Status = "狀態",
        Brand = "廠牌", Model = "型號", Serial = "序號", PurchaseDate = "購入日期", Cost = "購入成本",
        Salvage = "殘值", Life = "耐用年限", Warranty = "保固到期日", LocationCode = "地點代碼",
        DepartmentCode = "部門代碼", Custodian = "保管人帳號", Vendor = "廠商名稱", UsageUnit = "使用量單位",
        Usage = "目前使用量", Description = "說明";

    /// <summary>匯入與匯出共用的欄位與順序。</summary>
    private static readonly (string Header, bool Required, string Hint)[] Columns =
    [
        (AssetNo, false, "留白由系統自動配號；自行填寫時格式為 AST-類別代碼-0001 且不可重複"),
        (Name, true, ""),
        (Category, true, "見「代碼對照」工作表"),
        (Status, false, "使用中或閒置，留白視為使用中"),
        (Brand, false, ""),
        (Model, false, ""),
        (Serial, false, "不可與現有資產重複"),
        (PurchaseDate, true, "yyyy-MM-dd"),
        (Cost, true, "數字"),
        (Salvage, false, "留白視為 0"),
        (Life, false, "年；留白採用類別預設值"),
        (Warranty, false, "yyyy-MM-dd"),
        (LocationCode, true, "見「代碼對照」工作表"),
        (DepartmentCode, false, "見「代碼對照」工作表"),
        (Custodian, false, "使用者帳號"),
        (Vendor, false, "需與廠商資料名稱一致"),
        (UsageUnit, false, ""),
        (Usage, false, "數字"),
        (Description, false, "")
    ];

    public byte[] Export(AssetQuery query)
    {
        currentUser.RequireManager();
        var items = assetService.Filter(query);

        var byId = assets.GetAll().ToDictionary(a => a.Id);
        var categoryById = categories.GetAll().ToDictionary(c => c.Id);
        var locationById = locations.GetAll().ToDictionary(l => l.Id);
        var departmentById = departments.GetAll().ToDictionary(d => d.Id);
        var userById = users.GetAll().ToDictionary(u => u.Id);
        var vendorById = vendors.GetAll().ToDictionary(v => v.Id);

        var headers = Columns.Select(c => c.Header)
            .Concat(["類別名稱", "地點名稱", "部門名稱", "保管人", "保固狀態", "累計折舊", "帳面價值", "報廢時間", "報廢原因"])
            .ToList();

        var rows = items.Where(i => byId.ContainsKey(i.Id)).Select(item =>
        {
            var a = byId[item.Id];
            var category = categoryById.GetValueOrDefault(a.CategoryId);
            var location = locationById.GetValueOrDefault(a.LocationId);
            var department = a.DepartmentId is { } d ? departmentById.GetValueOrDefault(d) : null;
            var custodian = a.CustodianUserId is { } u ? userById.GetValueOrDefault(u) : null;
            var depreciation = DepreciationCalculator.Calculate(a.PurchaseCost, a.SalvageValue, a.UsefulLifeYears,
                a.PurchaseDate, AssetService.DepreciationDate(a, time));

            return (IReadOnlyList<object?>)
            [
                a.AssetNo, a.Name, category?.Code, a.Status.DisplayName(), a.Brand, a.Model, a.SerialNo,
                a.PurchaseDate, a.PurchaseCost, a.SalvageValue, a.UsefulLifeYears, a.WarrantyEndDate,
                location?.Code, department?.Code, custodian?.UserName,
                a.VendorId is { } v ? vendorById.GetValueOrDefault(v)?.Name : null,
                a.UsageUnit, a.CurrentUsage, a.Description,
                category?.Name, location?.Name, department?.Name, custodian?.DisplayName,
                item.Warranty.DisplayName(),
                depreciation.AccumulatedAmount, depreciation.BookValue,
                a.ScrappedAt is { } at ? time.ToLocal(at) : null, a.ScrapReason
            ];
        }).ToList();

        audit.Record(AuditActions.Export, nameof(Asset), detail: $"匯出資產清冊 {rows.Count} 筆");
        return spreadsheet.Write(new SheetData("資產清冊", headers, rows));
    }

    public byte[] CreateImportTemplate()
    {
        currentUser.RequireManager();
        var instructions = Columns
            .Select(c => (IReadOnlyList<object?>)[c.Header, c.Required ? "必填" : "選填", c.Hint]).ToList();
        var codes = categories.Query(c => c.IsActive).OrderBy(c => c.Code)
            .Select(c => (IReadOnlyList<object?>)["類別", c.Code, c.Name])
            .Concat(locations.Query(l => l.IsActive).OrderBy(l => l.SortOrder)
                .Select(l => (IReadOnlyList<object?>)["地點", l.Code, l.Name]))
            .Concat(departments.Query(d => d.IsActive).OrderBy(d => d.Code)
                .Select(d => (IReadOnlyList<object?>)["部門", d.Code, d.Name]))
            .ToList();

        return spreadsheet.Write(
            new SheetData("匯入資料", Columns.Select(c => c.Header).ToList(), []),
            new SheetData("欄位說明", ["欄位", "必填", "說明"], instructions),
            new SheetData("代碼對照", ["種類", "代碼", "名稱"], codes));
    }

    public ImportResult Import(UploadedFile file)
    {
        currentUser.RequireManager();
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("僅支援 .xlsx 檔案。");
        var maxMb = settings.GetInt(SettingKeys.UploadMaxSizeMb, 10);
        if (file.Length <= 0 || file.Length > maxMb * 1024L * 1024L)
            throw new BusinessRuleException($"檔案不可為空且不可超過 {maxMb} MB。");

        var sheet = spreadsheet.Read(file.Content, MaxImportRows);
        var missing = Columns.Where(c => c.Required && !sheet.Headers.Contains(c.Header)).Select(c => c.Header).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException($"缺少必要欄位：{string.Join("、", missing)}。請使用匯入範本。");
        if (sheet.Rows.Count == 0) throw new BusinessRuleException("檔案內沒有資料列。");

        return businessLock.Run(() => ImportRows(sheet.Rows));
    }

    /// <summary>先驗證全部資料列，全部通過才寫入。</summary>
    private ImportResult ImportRows(IReadOnlyList<SheetRow> rows)
    {
        var categoryByCode = categories.GetAll().ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var locationByCode = locations.GetAll().ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);
        var departmentByCode = departments.GetAll().ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        var userByName = users.GetAll().ToDictionary(u => u.UserName, StringComparer.OrdinalIgnoreCase);
        var vendorByName = vendors.GetAll().GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.First());
        var existing = assets.GetAll();
        var numbers = existing.Select(a => a.AssetNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var serials = existing.Where(a => a.SerialNo is not null).Select(a => a.SerialNo!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<ImportError>();
        var pending = new List<(Asset Asset, string CategoryCode)>();

        foreach (var row in rows)
        {
            try
            {
                var category = Lookup(categoryByCode, row.Get(Category), Category)
                               ?? throw new BusinessRuleException($"請填寫{Category}。");
                var location = Lookup(locationByCode, row.Get(LocationCode), LocationCode)
                               ?? throw new BusinessRuleException($"請填寫{LocationCode}。");
                var statusText = row.Get(Status);
                var status = statusText.Length == 0 || statusText == AssetStatus.InUse.DisplayName() ? AssetStatus.InUse
                    : statusText == AssetStatus.Idle.DisplayName() ? AssetStatus.Idle
                    : throw new BusinessRuleException("狀態只能是使用中或閒置。");

                var asset = assetService.BuildNew(new AssetInput(
                    row.Get(Name), category.Id, status, row.Get(Brand), row.Get(Model), row.Get(Serial),
                    ParseDate(row, PurchaseDate) ?? throw new BusinessRuleException($"請填寫{PurchaseDate}。"),
                    ParseNumber(row, Cost) ?? throw new BusinessRuleException($"請填寫{Cost}。"),
                    ParseNumber(row, Salvage) ?? 0,
                    ParseInteger(row, Life) ?? category.DefaultUsefulLifeYears,
                    ParseDate(row, Warranty), location.Id,
                    Lookup(departmentByCode, row.Get(DepartmentCode), DepartmentCode)?.Id,
                    Lookup(userByName, row.Get(Custodian), Custodian)?.Id,
                    Lookup(vendorByName, row.Get(Vendor), Vendor)?.Id,
                    row.Get(Description), row.Get(UsageUnit), ParseNumber(row, Usage)));

                if (row.Get(AssetNo) is { Length: > 0 } no)
                {
                    no = no.ToUpperInvariant();
                    if (AssetRules.ParseSequence(no, category.Code) is not > 0)
                        throw new BusinessRuleException($"資產編號格式需為 AST-{category.Code}-0001。");
                    if (!numbers.Add(no)) throw new BusinessRuleException($"資產編號 {no} 已存在或在檔案中重複。");
                    asset.AssetNo = no;
                }
                if (asset.SerialNo is { } serial && !serials.Add(serial))
                    throw new BusinessRuleException($"序號 {serial} 已存在或在檔案中重複。");

                pending.Add((asset, category.Code));
            }
            catch (BusinessRuleException ex)
            {
                errors.Add(new ImportError(row.RowNumber, ex.Message));
            }
        }

        if (errors.Count > 0) return new ImportResult([], errors);

        // 先寫入指定編號者，自動配號才會接在其後。
        var created = new List<string>();
        foreach (var (asset, code) in pending.OrderBy(p => p.Asset.AssetNo.Length == 0))
        {
            if (asset.AssetNo.Length == 0) asset.AssetNo = assetService.NextAssetNo(code);
            assets.Add(asset);
            created.Add(asset.AssetNo);
        }
        created.Sort(StringComparer.Ordinal);
        audit.Record(AuditActions.Import, nameof(Asset),
            detail: $"Excel 匯入資產 {created.Count} 筆：{string.Join("、", created.Take(20))}{(created.Count > 20 ? "…" : "")}");
        return new ImportResult(created, []);
    }

    private static T? Lookup<T>(IReadOnlyDictionary<string, T> map, string key, string label) where T : class =>
        key.Length == 0 ? null
        : map.TryGetValue(key, out var value) ? value
        : throw new BusinessRuleException($"找不到{label}「{key}」。");

    private static DateOnly? ParseDate(SheetRow row, string header)
    {
        var text = row.Get(header);
        if (text.Length == 0) return null;
        return DateOnly.TryParseExact(text, ["yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : throw new BusinessRuleException($"{header}「{text}」不是有效日期（yyyy-MM-dd）。");
    }

    private static decimal? ParseNumber(SheetRow row, string header)
    {
        var text = row.Get(header);
        if (text.Length == 0) return null;
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new BusinessRuleException($"{header}「{text}」不是有效數字。");
    }

    private static int? ParseInteger(SheetRow row, string header) => ParseNumber(row, header) switch
    {
        null => null,
        { } n when n == decimal.Truncate(n) && n is >= int.MinValue and <= int.MaxValue => (int)n,
        _ => throw new BusinessRuleException($"{header}需為整數。")
    };
}
