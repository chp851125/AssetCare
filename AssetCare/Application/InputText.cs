using AssetCare.Domain.Exceptions;

namespace AssetCare.Application;

/// <summary>Service 層的文字輸入正規化與驗證。</summary>
public static class InputText
{
    public static string Required(string? value, string label, int maxLength) =>
        Optional(value, label, maxLength) ?? throw new BusinessRuleException($"請輸入{label}。");

    public static string? Optional(string? value, string label, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length <= maxLength
            ? text
            : throw new BusinessRuleException($"{label}不可超過 {maxLength} 個字。");
    }
}
