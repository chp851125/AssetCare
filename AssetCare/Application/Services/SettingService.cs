using System.Globalization;
using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Application.Services;

public class SettingService(
    IRepository<AppSetting> settings,
    IBusinessLock businessLock,
    ICurrentUser currentUser,
    IAuditService audit) : ISettingService
{
    private record Definition(string Group, SettingKind Kind, int Min = 0, int Max = 0);

    private const string Sla = "SLA 時限", Order = "工單與保養", Upload = "檔案上傳", Security = "登入安全";

    /// <summary>可修改的參數與允許範圍；顯示順序即宣告順序。</summary>
    private static readonly List<(string Key, Definition Rule)> Definitions =
    [
        .. new[] { Priority.Urgent, Priority.High, Priority.Normal, Priority.Low }.SelectMany(p => new[]
        {
            (SettingKeys.SlaResponseHours(p), new Definition(Sla, SettingKind.Number, 1, 8760)),
            (SettingKeys.SlaResolveHours(p), new Definition(Sla, SettingKind.Number, 1, 8760))
        }),
        (SettingKeys.AutoSetAssetUnderRepair, new Definition(Order, SettingKind.Boolean)),
        (SettingKeys.DuplicateReportDays, new Definition(Order, SettingKind.Number, 1, 90)),
        (SettingKeys.MaintenanceDefaultLeadDays, new Definition(Order, SettingKind.Number, 0, 364)),
        (SettingKeys.WarrantyNoticeDays, new Definition(Order, SettingKind.Number, 1, 365)),
        // 上限配合含多檔上傳的要求大小限制（55 MB）。
        (SettingKeys.UploadMaxSizeMb, new Definition(Upload, SettingKind.Number, 1, 10)),
        (SettingKeys.UploadAllowedExtensions, new Definition(Upload, SettingKind.Extensions)),
        (SettingKeys.MaxFailedLogins, new Definition(Security, SettingKind.Number, 3, 20)),
        (SettingKeys.LockoutMinutes, new Definition(Security, SettingKind.Number, 1, 1440)),
        (SettingKeys.PasswordMinLength, new Definition(Security, SettingKind.Number, 8, 64))
    ];

    public IReadOnlyList<SettingItem> GetAll()
    {
        currentUser.RequireAdmin();
        var byKey = settings.GetAll().ToDictionary(s => s.Key);
        return Definitions.Where(d => byKey.ContainsKey(d.Key)).Select(d =>
        {
            var s = byKey[d.Key];
            return new SettingItem(s.Id, s.Version, s.Key, d.Rule.Group, s.Description, s.Value, d.Rule.Kind,
                d.Rule.Min, d.Rule.Max);
        }).ToList();
    }

    public int Update(IReadOnlyList<SettingChange> changes)
    {
        currentUser.RequireAdmin();
        return businessLock.Run(() =>
        {
            var all = settings.GetAll().ToDictionary(s => s.Id);
            var values = all.Values.ToDictionary(s => s.Key, s => s.Value);
            var rules = Definitions.ToDictionary(d => d.Key, d => d.Rule);
            var pending = new List<(AppSetting Setting, string Value)>();

            foreach (var change in changes)
            {
                if (!all.TryGetValue(change.Id, out var setting) || !rules.TryGetValue(setting.Key, out var rule))
                    throw new BusinessRuleException("參數內容不一致，請重新整理頁面。");
                var value = Normalize(change.Value, rule, setting.Description);
                values[setting.Key] = value;
                if (value == setting.Value) continue;
                if (setting.Version != change.Version) throw new ConcurrencyException();
                pending.Add((setting, value));
            }
            EnsureSlaOrder(values);

            foreach (var (setting, value) in pending)
            {
                var previous = setting.Value;
                setting.Value = value;
                settings.Update(setting);
                audit.Record(AuditActions.Update, nameof(AppSetting), setting.Id, setting.Key,
                    $"{setting.Description}：{previous} → {value}");
            }
            return pending.Count;
        });
    }

    private static string Normalize(string? input, Definition rule, string label)
    {
        var text = input?.Trim() ?? "";
        switch (rule.Kind)
        {
            case SettingKind.Boolean:
                return text is "true" or "false" ? text : throw new BusinessRuleException($"請選擇「{label}」。");
            case SettingKind.Extensions:
                var extensions = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.ToLowerInvariant()).Distinct().ToList();
                if (extensions.Count == 0) throw new BusinessRuleException($"請填寫「{label}」。");
                var unknown = extensions.Where(e => !AttachmentService.IsKnownExtension(e)).ToList();
                if (unknown.Count > 0)
                    throw new BusinessRuleException(
                        $"不支援的副檔名：{string.Join("、", unknown)}。可用：{AttachmentService.KnownExtensionList}");
                return string.Join(",", extensions);
            default:
                return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
                       number >= rule.Min && number <= rule.Max
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : throw new BusinessRuleException($"「{label}」須為 {rule.Min} 到 {rule.Max} 的整數。");
        }
    }

    /// <summary>回應時限不可長於完工時限。</summary>
    private static void EnsureSlaOrder(Dictionary<string, string> values)
    {
        foreach (var priority in Enum.GetValues<Priority>())
        {
            if (int.TryParse(values.GetValueOrDefault(SettingKeys.SlaResponseHours(priority)), out var response) &&
                int.TryParse(values.GetValueOrDefault(SettingKeys.SlaResolveHours(priority)), out var resolve) &&
                response > resolve)
                throw new BusinessRuleException($"「{priority.DisplayName()}」優先級的回應時限不可長於完工時限。");
        }
    }
}
