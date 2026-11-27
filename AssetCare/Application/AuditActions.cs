namespace AssetCare.Application;

/// <summary>稽核動作代碼。新增稽核動作時在此登錄。</summary>
public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string QuickSwitch = "QuickSwitch";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Scrap = "Scrap";
    public const string Transfer = "Transfer";
    public const string Export = "Export";
    public const string ResetData = "ResetData";
    public const string Import = "Import";
    public const string Upload = "Upload";
    public const string ResetPassword = "ResetPassword";
    public const string Activate = "Activate";
    public const string Deactivate = "Deactivate";
    public const string Unlock = "Unlock";
    public const string Assign = "Assign";
    public const string CancelWorkOrder = "CancelWorkOrder";
    public const string StockIn = "StockIn";
    public const string AdjustStock = "AdjustStock";
    public const string GenerateMaintenance = "GenerateMaintenance";
    public const string RecordUsage = "RecordUsage";

    /// <summary>稽核查詢與匯出顯示的中文名稱；新增動作時一併登錄。</summary>
    private static readonly Dictionary<string, string> Labels = new()
    {
        [Login] = "登入", [LoginFailed] = "登入失敗", [Logout] = "登出", [QuickSwitch] = "快速切換使用者",
        [Create] = "新增", [Update] = "修改", [Delete] = "刪除", [Scrap] = "報廢", [Transfer] = "轉移",
        [Export] = "匯出", [ResetData] = "重置資料", [Import] = "匯入", [Upload] = "上傳",
        [ResetPassword] = "重設密碼", [Activate] = "啟用", [Deactivate] = "停用", [Unlock] = "解除鎖定",
        [Assign] = "派工", [CancelWorkOrder] = "取消工單", [StockIn] = "入庫", [AdjustStock] = "盤點調整",
        [GenerateMaintenance] = "產生保養工單", [RecordUsage] = "登錄使用量"
    };

    public static IReadOnlyDictionary<string, string> All => Labels;

    public static string Label(string action) => Labels.GetValueOrDefault(action, action);
}
