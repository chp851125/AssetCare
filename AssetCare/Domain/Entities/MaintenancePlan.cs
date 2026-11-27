using AssetCare.Domain.Enums;

namespace AssetCare.Domain.Entities;

public class MaintenancePlan : BaseEntity
{
    public string Name { get; set; } = "";
    public Guid AssetId { get; set; }
    public MaintenanceCycle Cycle { get; set; }
    public Priority Priority { get; set; } = Priority.Normal;
    /// <summary>下次保養日；依使用量的計畫為 null。</summary>
    public DateOnly? NextDueDate { get; set; }
    /// <summary>下次保養日前 N 天產生工單。</summary>
    public int LeadDays { get; set; }
    public DateOnly? LastCompletedDate { get; set; }
    /// <summary>依使用量：每累積多少用量保養一次。</summary>
    public decimal? UsageInterval { get; set; }
    public decimal? NextDueUsage { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public List<string> ChecklistItems { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
