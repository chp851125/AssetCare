namespace AssetCare.Application.Interfaces;

public interface IMaintenanceScheduler
{
    /// <summary>為到期的保養計畫產生工單，回傳新增張數。供背景作業呼叫，不依賴登入身分。</summary>
    int GenerateDueOrders();
}
