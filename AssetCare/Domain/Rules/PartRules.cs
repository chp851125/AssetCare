using AssetCare.Domain.Entities;

namespace AssetCare.Domain.Rules;

public static class PartRules
{
    /// <summary>啟用中且庫存低於安全量（等於安全量不算）。</summary>
    public static bool IsLowStock(Part part) => part.IsActive && part.StockQuantity < part.SafetyStock;
}
