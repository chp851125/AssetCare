using System.ComponentModel.DataAnnotations;

namespace AssetCare.Domain.Enums;

public enum UserRole
{
    [Display(Name = "申請人")] Requester,
    [Display(Name = "技術員")] Technician,
    [Display(Name = "主管")] Supervisor,
    [Display(Name = "系統管理員")] Admin
}

public enum AssetStatus
{
    [Display(Name = "使用中")] InUse,
    [Display(Name = "閒置")] Idle,
    [Display(Name = "維修中")] UnderRepair,
    [Display(Name = "已報廢")] Scrapped
}

public enum WorkOrderStatus
{
    [Display(Name = "待派工")] Open,
    [Display(Name = "已指派")] Assigned,
    [Display(Name = "處理中")] InProgress,
    [Display(Name = "待料中")] WaitingParts,
    [Display(Name = "待驗收")] Completed,
    [Display(Name = "已結案")] Closed,
    [Display(Name = "已取消")] Cancelled
}

public enum WorkOrderType
{
    [Display(Name = "報修")] Repair,
    [Display(Name = "預防保養")] Preventive
}

public enum Priority
{
    [Display(Name = "低")] Low,
    [Display(Name = "一般")] Normal,
    [Display(Name = "高")] High,
    [Display(Name = "緊急")] Urgent
}

public enum MaintenanceCycle
{
    [Display(Name = "每日")] Daily,
    [Display(Name = "每週")] Weekly,
    [Display(Name = "每月")] Monthly,
    [Display(Name = "每季")] Quarterly,
    [Display(Name = "每年")] Yearly,
    [Display(Name = "依使用量")] ByUsage
}

public enum WorkOrderAction
{
    [Display(Name = "建立工單")] Created,
    [Display(Name = "派工")] Assigned,
    [Display(Name = "改派")] Reassigned,
    [Display(Name = "接單")] Accepted,
    [Display(Name = "開始處理")] Started,
    [Display(Name = "待料")] WaitingParts,
    [Display(Name = "恢復處理")] Resumed,
    [Display(Name = "完工回報")] Completed,
    [Display(Name = "驗收通過")] Verified,
    [Display(Name = "驗收退回")] Rejected,
    [Display(Name = "取消工單")] Cancelled,
    [Display(Name = "留言")] Comment,
    [Display(Name = "外送維修")] SentToVendor,
    [Display(Name = "廠商送回")] ReturnedFromVendor,
    [Display(Name = "領用零件")] PartIssued,
    [Display(Name = "更新檢查清單")] ChecklistUpdated
}

public enum PartTransactionType
{
    [Display(Name = "入庫")] StockIn,
    [Display(Name = "領用")] Issue,
    [Display(Name = "退回")] Return,
    [Display(Name = "盤點調整")] Adjust
}

public enum NotificationType
{
    [Display(Name = "新報修")] NewRepair,
    [Display(Name = "派工通知")] WorkOrderAssigned,
    [Display(Name = "工單狀態變更")] WorkOrderStatusChanged,
    [Display(Name = "驗收結果")] VerificationResult,
    [Display(Name = "SLA 逾時")] SlaOverdue,
    [Display(Name = "保養即將到期")] MaintenanceDue,
    [Display(Name = "庫存低於安全量")] LowStock,
    [Display(Name = "保固即將到期")] WarrantyExpiring
}

public enum EmailStatus
{
    [Display(Name = "已寄送")] Sent,
    [Display(Name = "寄送失敗")] Failed
}

public enum AttachmentOwnerType
{
    [Display(Name = "資產")] Asset,
    [Display(Name = "工單")] WorkOrder
}

public enum WarrantyState
{
    [Display(Name = "未登錄保固")] None,
    [Display(Name = "保固中")] Active,
    [Display(Name = "保固即將到期")] Expiring,
    [Display(Name = "已過保固")] Expired
}

public enum MaintenanceDueState
{
    [Display(Name = "正常")] Normal,
    [Display(Name = "即將到期")] DueSoon,
    [Display(Name = "逾期")] Overdue,
    [Display(Name = "停用")] Inactive
}
