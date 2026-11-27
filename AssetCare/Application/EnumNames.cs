using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace AssetCare.Application;

public static class EnumNames
{
    /// <summary>[Display(Name)] 的中文名稱，供匯出匯入與訊息使用；畫面請用 .Label()。</summary>
    public static string DisplayName(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
}
