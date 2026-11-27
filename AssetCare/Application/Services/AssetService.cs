using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;
using AssetCare.Domain.Rules;

namespace AssetCare.Application.Services;

public class AssetService(
    IRepository<Asset> assets,
    IRepository<AssetCategory> categories,
    IRepository<Location> locations,
    IRepository<Department> departments,
    IRepository<User> users,
    IRepository<Vendor> vendors,
    IRepository<AssetTransfer> transfers,
    IRepository<WorkOrder> workOrders,
    IRepository<MaintenancePlan> plans,
    IRepository<Attachment> attachments,
    IRepository<AppSetting> settings,
    IQrCodeGenerator qrCodes,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit,
    ITimeProvider time) : IAssetService
{
    public const int PageSize = 10;
    public const int MaxLabels = 240;

    public PagedResult<AssetListItem> Search(AssetQuery query)
    {
        currentUser.RequireUserId();
        return PagedResult<AssetListItem>.From(Filter(query), query.Page, PageSize);
    }

    /// <summary>套用篩選與排序，回傳全部結果（不分頁）。匯出與標籤列印共用。</summary>
    internal List<AssetListItem> Filter(AssetQuery query)
    {
        var hierarchy = new LocationHierarchy(locations.GetAll());
        var categoryNames = categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var userNames = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);
        var noticeDays = WarrantyNoticeDays();
        var today = time.Today;

        IEnumerable<Asset> source = assets.GetAll();
        if (query.Keyword?.Trim() is { Length: > 0 } keyword)
            source = source.Where(a => new[] { a.AssetNo, a.Name, a.Brand, a.Model, a.SerialNo }
                .Any(v => v?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));
        if (query.CategoryId is { } categoryId) source = source.Where(a => a.CategoryId == categoryId);
        if (query.LocationId is { } locationId)
        {
            var scope = hierarchy.SelfAndDescendants(locationId);
            source = source.Where(a => scope.Contains(a.LocationId));
        }
        if (query.DepartmentId is { } departmentId) source = source.Where(a => a.DepartmentId == departmentId);
        if (query.Status is { } status) source = source.Where(a => a.Status == status);

        var items = source.Select(a => new AssetListItem(
            a.Id, a.AssetNo, a.Name, categoryNames.GetValueOrDefault(a.CategoryId, "—"), a.Status,
            hierarchy.NameOf(a.LocationId),
            a.CustodianUserId is { } c ? userNames.GetValueOrDefault(c) : null,
            a.PurchaseDate, a.WarrantyEndDate,
            WarrantyRules.StateOf(a.WarrantyEndDate, today, noticeDays)));
        if (query.Warranty is { } warranty) items = items.Where(i => i.Warranty == warranty);

        Func<AssetListItem, IComparable?> key = query.Sort switch
        {
            AssetSort.Name => i => i.Name,
            AssetSort.Category => i => i.CategoryName,
            AssetSort.Status => i => i.Status,
            AssetSort.Location => i => i.LocationName,
            AssetSort.PurchaseDate => i => i.PurchaseDate,
            // 未登錄保固者一律排在最後
            AssetSort.Warranty => i => i.WarrantyEndDate ?? (query.Desc ? DateOnly.MinValue : DateOnly.MaxValue),
            _ => i => i.AssetNo
        };
        var ordered = query.Desc ? items.OrderByDescending(key) : items.OrderBy(key);
        return ordered.ThenBy(i => i.AssetNo, StringComparer.Ordinal).ToList();
    }

    public AssetFilterOptions GetFilterOptions()
    {
        currentUser.RequireUserId();
        return new AssetFilterOptions(
            categories.GetAll().OrderBy(c => c.Code).Select(c => new Option(c.Id, c.Name)).ToList(),
            new LocationHierarchy(locations.GetAll()).Ordered()
                .Select(n => new Option(n.Location.Id, LocationHierarchy.Indent(n.Location.Name, n.Depth))).ToList(),
            departments.GetAll().OrderBy(d => d.Code).Select(d => new Option(d.Id, d.Name)).ToList());
    }

    public AssetDetail GetDetail(Guid id)
    {
        var userId = currentUser.RequireUserId();
        var asset = Find(id);
        var isStaff = currentUser.IsStaff();
        var hierarchy = new LocationHierarchy(locations.GetAll());
        var userNames = users.GetAll().ToDictionary(u => u.Id, u => u.DisplayName);
        var departmentNames = departments.GetAll().ToDictionary(d => d.Id, d => d.Name);
        string? UserName(Guid? u) => u is { } value ? userNames.GetValueOrDefault(value) : null;
        string? DepartmentName(Guid? d) => d is { } value ? departmentNames.GetValueOrDefault(value) : null;
        var today = time.Today;
        var vendor = asset.VendorId is { } vendorId ? vendors.GetById(vendorId) : null;

        AssetFinance? finance = null;
        if (currentUser.IsManager())
        {
            var asOf = DepreciationDate(asset, time);
            finance = new AssetFinance(asset.PurchaseCost, asset.SalvageValue, asset.UsefulLifeYears,
                DepreciationCalculator.Calculate(
                    asset.PurchaseCost, asset.SalvageValue, asset.UsefulLifeYears, asset.PurchaseDate, asOf),
                asOf);
        }

        var orders = workOrders.Query(w => w.AssetId == id)
            .Where(w => isStaff || w.RequesterUserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new AssetWorkOrderItem(w.Id, w.OrderNo, w.Type, w.Status, w.Priority, w.Title,
                UserName(w.AssigneeUserId), w.CreatedAt))
            .ToList();

        var planItems = plans.Query(p => p.AssetId == id)
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.NextDueDate)
            .Select(p => new AssetPlanItem(p.Id, p.Name, p.Cycle, p.NextDueDate, p.NextDueUsage, p.LastCompletedDate,
                UserName(p.AssigneeUserId), p.IsActive,
                p.IsActive && (p.NextDueDate < today ||
                               (p.Cycle == MaintenanceCycle.ByUsage && asset.CurrentUsage >= p.NextDueUsage))))
            .ToList();

        string Describe(Guid? location, Guid? department, Guid? custodian) => string.Join("｜",
            location is { } l ? hierarchy.NameOf(l) : "—",
            DepartmentName(department) ?? "無部門",
            UserName(custodian) ?? "無保管人");

        List<AssetTransferItem> transferItems = !isStaff ? [] : transfers.Query(t => t.AssetId == id)
            .OrderByDescending(t => t.TransferredAt)
            .Select(t => new AssetTransferItem(t.TransferredAt,
                Describe(t.FromLocationId, t.FromDepartmentId, t.FromCustodianUserId),
                Describe(t.ToLocationId, t.ToDepartmentId, t.ToCustodianUserId),
                t.Reason, UserName(t.TransferredByUserId) ?? "—"))
            .ToList();

        List<AttachmentItem> attachmentItems = !isStaff ? [] : attachments
            .Query(a => a.OwnerType == AttachmentOwnerType.Asset && a.OwnerId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentItem(a.Id, a.FileName, a.Caption, a.ContentType, a.SizeBytes, a.CreatedAt,
                UserName(a.UploadedByUserId) ?? "—", currentUser.IsManager() || a.UploadedByUserId == userId))
            .ToList();

        return new AssetDetail(asset.Id, asset.Version, asset.AssetNo, asset.Name,
            categories.GetById(asset.CategoryId)?.Name ?? "—", asset.Status,
            asset.Brand, asset.Model, asset.SerialNo, asset.Description,
            asset.PurchaseDate, asset.WarrantyEndDate,
            WarrantyRules.StateOf(asset.WarrantyEndDate, today, WarrantyNoticeDays()),
            asset.WarrantyEndDate is { } end ? end.DayNumber - today.DayNumber : null,
            hierarchy.PathOf(asset.LocationId), DepartmentName(asset.DepartmentId), UserName(asset.CustodianUserId),
            vendor?.Name, vendor?.Phone, asset.CurrentUsage, asset.UsageUnit,
            asset.ScrappedAt, asset.ScrapReason,
            finance, orders, planItems, transferItems, attachmentItems);
    }

    /// <summary>折舊計算基準日（台北日期）；報廢資產停在報廢當天。</summary>
    internal static DateOnly DepreciationDate(Asset asset, ITimeProvider time) =>
        asset.ScrappedAt is { } scrapped ? DateOnly.FromDateTime(time.ToLocal(scrapped)) : time.Today;

    public Guid GetIdByAssetNo(string assetNo)
    {
        currentUser.RequireUserId();
        var no = assetNo.Trim();
        return assets.GetAll().FirstOrDefault(a => string.Equals(a.AssetNo, no, StringComparison.OrdinalIgnoreCase))?.Id
               ?? throw new NotFoundException("找不到此資產編號。");
    }

    public AssetFormOptions GetFormOptions()
    {
        currentUser.RequireManager();
        return new AssetFormOptions(
            categories.Query(c => c.IsActive).OrderBy(c => c.Code)
                .Select(c => new CategoryOption(c.Id, c.Code, c.Name, c.DefaultUsefulLifeYears)).ToList(),
            new LocationHierarchy(locations.GetAll()).Ordered().Where(n => n.Location.IsActive)
                .Select(n => new Option(n.Location.Id, LocationHierarchy.Indent(n.Location.Name, n.Depth))).ToList(),
            departments.Query(d => d.IsActive).OrderBy(d => d.Code).Select(d => new Option(d.Id, d.Name)).ToList(),
            users.Query(u => u.IsActive).OrderBy(u => u.UserName)
                .Select(u => new Option(u.Id, $"{u.DisplayName}（{u.UserName}）")).ToList(),
            vendors.GetAll().OrderByDescending(v => v.IsActive).ThenBy(v => v.Name)
                .Select(v => new Option(v.Id, v.IsActive ? v.Name : $"{v.Name}（停用）")).ToList());
    }

    public AssetEditData GetForEdit(Guid id)
    {
        currentUser.RequireManager();
        var a = Find(id);
        AssetRules.EnsureNotScrapped(a, "編輯");
        return new AssetEditData(a.Id, a.Version, a.AssetNo, new AssetInput(a.Name, a.CategoryId, a.Status, a.Brand,
            a.Model, a.SerialNo, a.PurchaseDate, a.PurchaseCost, a.SalvageValue, a.UsefulLifeYears, a.WarrantyEndDate,
            a.LocationId, a.DepartmentId, a.CustodianUserId, a.VendorId, a.Description, a.UsageUnit, a.CurrentUsage));
    }

    public Guid Create(AssetInput input)
    {
        currentUser.RequireManager();
        return businessLock.Run(() =>
        {
            var asset = BuildNew(input);
            EnsureSerialUnused(asset);
            asset.AssetNo = NextAssetNo(categories.GetById(asset.CategoryId)!.Code);
            assets.Add(asset);
            audit.Record(AuditActions.Create, nameof(Asset), asset.Id, asset.AssetNo, $"新增資產「{asset.Name}」");
            return asset.Id;
        });
    }

    /// <summary>驗證輸入並建立尚未配號、尚未儲存的資產。新增與匯入共用。</summary>
    internal Asset BuildNew(AssetInput input)
    {
        if (categories.GetById(input.CategoryId) is not { IsActive: true })
            throw new BusinessRuleException("請選擇啟用中的資產類別。");
        if (input.Status is not (AssetStatus.InUse or AssetStatus.Idle))
            throw new BusinessRuleException("新增資產的狀態只能是使用中或閒置。");
        EnsurePlacement(input.LocationId, input.DepartmentId, input.CustodianUserId);

        var asset = new Asset
        {
            CategoryId = input.CategoryId,
            Status = input.Status,
            LocationId = input.LocationId,
            DepartmentId = input.DepartmentId,
            CustodianUserId = input.CustodianUserId
        };
        Apply(asset, input);
        return asset;
    }

    /// <summary>同類別現有最大序號 +1。須在 IBusinessLock 內呼叫。</summary>
    internal string NextAssetNo(string categoryCode)
    {
        var max = assets.GetAll().Max(a => AssetRules.ParseSequence(a.AssetNo, categoryCode)) ?? 0;
        return AssetRules.FormatAssetNo(categoryCode, max + 1);
    }

    public void Update(Guid id, int version, AssetInput input)
    {
        currentUser.RequireManager();
        businessLock.Run(() =>
        {
            var asset = Find(id);
            AssetRules.EnsureNotScrapped(asset, "編輯");
            // 維修中由工單控制；其他狀態只能在使用中與閒置之間切換。
            if (asset.Status != AssetStatus.UnderRepair && input.Status != asset.Status)
            {
                if (input.Status is not (AssetStatus.InUse or AssetStatus.Idle))
                    throw new BusinessRuleException("資產狀態只能改為使用中或閒置；維修與報廢請使用對應功能。");
                asset.Status = input.Status;
            }
            var oldSerial = asset.SerialNo;
            Apply(asset, input);
            // 僅在序號變更時檢查，既有的重複資料不阻擋其他修改。
            if (!string.Equals(oldSerial, asset.SerialNo, StringComparison.OrdinalIgnoreCase)) EnsureSerialUnused(asset);
            asset.Version = version;
            assets.Update(asset);
            audit.Record(AuditActions.Update, nameof(Asset), asset.Id, asset.AssetNo, "修改資產資料");
        });
    }

    public AssetSummary GetSummary(Guid id)
    {
        currentUser.RequireManager();
        var a = Find(id);
        var hierarchy = new LocationHierarchy(locations.GetAll());
        return new AssetSummary(a.Id, a.Version, a.AssetNo, a.Name, a.Status,
            a.LocationId, hierarchy.PathOf(a.LocationId),
            a.DepartmentId, a.DepartmentId is { } d ? departments.GetById(d)?.Name : null,
            a.CustodianUserId, a.CustodianUserId is { } u ? users.GetById(u)?.DisplayName : null,
            ActiveWorkOrders(id).Count);
    }

    public void Scrap(Guid id, int version, string? reason)
    {
        currentUser.RequireManager();
        var text = InputText.Required(reason, "報廢原因", 500);
        businessLock.Run(() =>
        {
            var asset = Find(id);
            AssetRules.EnsureNotScrapped(asset, "重複報廢");
            var active = ActiveWorkOrders(id);
            if (active.Count > 0)
                throw new BusinessRuleException(
                    $"尚有未結案工單（{string.Join("、", active.Select(w => w.OrderNo))}），請先結案或取消後再報廢。");

            asset.Status = AssetStatus.Scrapped;
            asset.ScrappedAt = time.UtcNow;
            asset.ScrapReason = text;
            asset.Version = version;
            assets.Update(asset);

            // 報廢資產不再需要保養。
            foreach (var plan in plans.Query(p => p.AssetId == id && p.IsActive))
            {
                plan.IsActive = false;
                plans.Update(plan);
            }
            audit.Record(AuditActions.Scrap, nameof(Asset), asset.Id, asset.AssetNo, $"報廢原因：{text}");
        });
    }

    public void Transfer(Guid id, int version, TransferInput input)
    {
        currentUser.RequireManager();
        var reason = InputText.Required(input.Reason, "轉移原因", 500);
        businessLock.Run(() =>
        {
            var asset = Find(id);
            AssetRules.EnsureNotScrapped(asset, "轉移");
            if (input.LocationId == asset.LocationId && input.DepartmentId == asset.DepartmentId &&
                input.CustodianUserId == asset.CustodianUserId)
                throw new BusinessRuleException("地點、部門與保管人皆未變更。");
            // 僅檢查有變更的欄位，原本的停用地點或人員不阻擋其他欄位轉移。
            EnsurePlacement(
                input.LocationId != asset.LocationId ? input.LocationId : null,
                input.DepartmentId != asset.DepartmentId ? input.DepartmentId : null,
                input.CustodianUserId != asset.CustodianUserId ? input.CustodianUserId : null);

            var record = new AssetTransfer
            {
                AssetId = asset.Id,
                FromLocationId = asset.LocationId,
                ToLocationId = input.LocationId,
                FromDepartmentId = asset.DepartmentId,
                ToDepartmentId = input.DepartmentId,
                FromCustodianUserId = asset.CustodianUserId,
                ToCustodianUserId = input.CustodianUserId,
                Reason = reason,
                TransferredByUserId = currentUser.RequireUserId(),
                TransferredAt = time.UtcNow
            };

            asset.LocationId = input.LocationId;
            asset.DepartmentId = input.DepartmentId;
            asset.CustodianUserId = input.CustodianUserId;
            asset.Version = version;
            assets.Update(asset); // 版本衝突時不留下轉移紀錄
            transfers.Add(record);
            audit.Record(AuditActions.Transfer, nameof(Asset), asset.Id, asset.AssetNo, $"轉移原因：{reason}");
        });
    }

    public IReadOnlyList<AssetLabel> GetLabels(IReadOnlyCollection<Guid> ids, AssetQuery query, string baseUrl)
    {
        currentUser.RequireStaff();
        var selected = ids.Count > 0
            ? Filter(new AssetQuery()).Where(a => ids.Contains(a.Id)).ToList()
            : Filter(query).Where(a => a.Status != AssetStatus.Scrapped).ToList();
        if (selected.Count > MaxLabels)
            throw new BusinessRuleException($"一次最多列印 {MaxLabels} 張標籤，請縮小篩選範圍。");
        return selected.Select(a =>
        {
            var url = $"{baseUrl.TrimEnd('/')}/Assets/ByNo/{Uri.EscapeDataString(a.AssetNo)}";
            return new AssetLabel(a.Id, a.AssetNo, a.Name, a.LocationName, url, qrCodes.CreatePng(url));
        }).ToList();
    }

    private Asset Find(Guid id) => assets.GetById(id) ?? throw new NotFoundException("找不到資產。");

    private List<WorkOrder> ActiveWorkOrders(Guid assetId) => workOrders.Query(w => w.AssetId == assetId)
        .Where(w => WorkOrderStateMachine.IsActive(w.Status)).OrderBy(w => w.OrderNo).ToList();

    private int WarrantyNoticeDays() => settings.GetInt(SettingKeys.WarrantyNoticeDays, 30);

    private void EnsureSerialUnused(Asset asset)
    {
        if (asset.SerialNo is not { } serial) return;
        var other = assets.GetAll().FirstOrDefault(a =>
            a.Id != asset.Id && string.Equals(a.SerialNo, serial, StringComparison.OrdinalIgnoreCase));
        if (other is not null) throw new BusinessRuleException($"序號 {serial} 已登錄於 {other.AssetNo}。");
    }

    private void EnsurePlacement(Guid? locationId, Guid? departmentId, Guid? custodianId)
    {
        if (locationId is { } l && locations.GetById(l) is not { IsActive: true })
            throw new BusinessRuleException("請選擇啟用中的地點。");
        if (departmentId is { } d && departments.GetById(d) is not { IsActive: true })
            throw new BusinessRuleException("請選擇啟用中的部門。");
        if (custodianId is { } u && users.GetById(u) is not { IsActive: true })
            throw new BusinessRuleException("保管人必須是啟用中的使用者。");
    }

    /// <summary>套用可編輯欄位（不含類別、狀態、地點、部門、保管人）。</summary>
    private void Apply(Asset asset, AssetInput input)
    {
        if (input.PurchaseDate > time.Today) throw new BusinessRuleException("購入日期不可晚於今天。");
        if (input.PurchaseDate.Year < 1950) throw new BusinessRuleException("購入日期不合理。");
        if (input.PurchaseCost is < 0 or > 1_000_000_000_000m) throw new BusinessRuleException("購入成本超出合理範圍。");
        if (input.SalvageValue < 0 || input.SalvageValue > input.PurchaseCost)
            throw new BusinessRuleException("殘值必須介於 0 與購入成本之間。");
        if (input.UsefulLifeYears is < 1 or > 100) throw new BusinessRuleException("耐用年限須為 1 到 100 年。");
        if (input.WarrantyEndDate < input.PurchaseDate) throw new BusinessRuleException("保固到期日不可早於購入日期。");
        if (input.CurrentUsage < 0) throw new BusinessRuleException("目前使用量不可為負數。");
        if (input.VendorId is { } v && vendors.GetById(v) is null) throw new BusinessRuleException("找不到指定的廠商。");

        asset.Name = InputText.Required(input.Name, "資產名稱", 100);
        asset.Brand = InputText.Optional(input.Brand, "廠牌", 50);
        asset.Model = InputText.Optional(input.Model, "型號", 50);
        asset.SerialNo = InputText.Optional(input.SerialNo, "序號", 80);
        asset.Description = InputText.Optional(input.Description, "說明", 1000);
        asset.UsageUnit = InputText.Optional(input.UsageUnit, "使用量單位", 20);
        asset.CurrentUsage = input.CurrentUsage;
        asset.PurchaseDate = input.PurchaseDate;
        asset.PurchaseCost = input.PurchaseCost;
        asset.SalvageValue = input.SalvageValue;
        asset.UsefulLifeYears = input.UsefulLifeYears;
        asset.WarrantyEndDate = input.WarrantyEndDate;
        asset.VendorId = input.VendorId;
    }
}
