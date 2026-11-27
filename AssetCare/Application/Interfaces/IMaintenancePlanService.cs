using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

public interface IMaintenancePlanService
{
    PagedResult<PlanListItem> Search(PlanQuery query);
    PlanDetail GetDetail(Guid id);
    PlanEditData GetForEdit(Guid id);
    PlanFormOptions GetFormOptions();
    Guid Create(PlanInput input);
    void Update(Guid id, int version, PlanInput input);
    void SetActive(Guid id, int version, bool isActive);
    /// <summary>登錄資產累計使用量；回傳因此產生的工單數。</summary>
    int RecordUsage(Guid planId, int assetVersion, decimal usage);
    /// <summary>立即檢查所有計畫並產生到期工單。</summary>
    int GenerateNow();
    /// <summary>行事曆事件；end 不含當天。</summary>
    IReadOnlyList<CalendarEvent> GetCalendar(DateOnly start, DateOnly end);
}
