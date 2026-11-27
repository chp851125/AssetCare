using AssetCare.Domain.Entities;
using AssetCare.Domain.Enums;
using AssetCare.Domain.Exceptions;

namespace AssetCare.Domain.Rules;

public static class AssetRules
{
    public static string FormatAssetNo(string categoryCode, int sequence) => $"AST-{categoryCode}-{sequence:0000}";

    /// <summary>取出指定類別資產編號的序號；格式不符回傳 null。</summary>
    public static int? ParseSequence(string assetNo, string categoryCode)
    {
        var prefix = $"AST-{categoryCode}-";
        if (!assetNo.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var digits = assetNo[prefix.Length..];
        return digits.Length is >= 4 and <= 8 && digits.All(char.IsAsciiDigit) ? int.Parse(digits) : null;
    }

    /// <summary>已報廢資產不可編輯、轉移、再次報廢或開立工單。</summary>
    public static void EnsureNotScrapped(Asset asset, string action)
    {
        if (asset.Status == AssetStatus.Scrapped)
            throw new BusinessRuleException($"資產 {asset.AssetNo} 已報廢，不可{action}。");
    }
}
