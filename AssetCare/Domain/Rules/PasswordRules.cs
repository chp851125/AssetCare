namespace AssetCare.Domain.Rules;

public static class PasswordRules
{
    public const int MaxLength = 100;

    /// <summary>回傳不符原則的原因；符合時回傳 null。</summary>
    public static string? Validate(string password, string userName, int minLength)
    {
        if (password.Length < minLength) return $"密碼至少需要 {minLength} 個字元。";
        if (password.Length > MaxLength) return $"密碼不可超過 {MaxLength} 個字元。";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "密碼需同時包含英文字母與數字。";
        if (userName.Length > 0 && password.Contains(userName, StringComparison.OrdinalIgnoreCase))
            return "密碼不可包含帳號。";
        return null;
    }
}
