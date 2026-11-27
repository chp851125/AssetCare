using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AssetCare.Application.Interfaces;

namespace AssetCare.Web.Infrastructure;

/// <summary>TempData 訊息鍵值，由 _Alerts 顯示。</summary>
public static class Flash
{
    public const string Success = "Flash.Success";
    public const string Error = "Flash.Error";
}

public static class ViewExtensions
{
    /// <summary>列舉的中文顯示名稱。</summary>
    public static string Label(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();

    /// <summary>UTC → 台北時間顯示字串。</summary>
    public static string Format(this ITimeProvider time, DateTime? utc, string format = "yyyy/MM/dd HH:mm") =>
        utc is { } value ? time.ToLocal(value).ToString(format) : "—";
}
