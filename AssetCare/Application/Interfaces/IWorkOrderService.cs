using AssetCare.Application.Dtos;

namespace AssetCare.Application.Interfaces;

/// <summary>工單查詢。資料範圍依目前使用者裁切。</summary>
public interface IWorkOrderService
{
    WorkOrderList Search(WorkOrderQuery query);
    WorkOrderDetail GetDetail(Guid id);
    /// <summary>可報修的資產（不含已報廢）。</summary>
    IReadOnlyList<Option> GetReportableAssets();
    /// <summary>已報廢資產丟出 BusinessRuleException。</summary>
    ReportContext GetReportContext(Guid assetId);
    /// <summary>僅限可處理該工單且狀態允許完工者。</summary>
    CompleteContext GetCompleteContext(Guid id);
}
