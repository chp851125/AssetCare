namespace AssetCare.Domain.Exceptions;

/// <summary>違反商業規則；訊息會直接顯示給使用者。</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>樂觀並行控制衝突。</summary>
public class ConcurrencyException(string message = "資料已被其他人修改，請重新整理頁面後再試一次。") : Exception(message);

public class NotFoundException(string message = "找不到指定的資料。") : Exception(message);

public class ForbiddenException(string message = "您沒有權限執行此操作。") : Exception(message);
