using AssetCare.Application.Dtos;
using AssetCare.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssetCare.Web.Infrastructure;

public static class FormExtensions
{
    /// <summary>
    /// 表單送出用：違反商業規則時把訊息放進 ModelState 並回傳 false，讓畫面保留使用者輸入。
    /// 其他例外（含並行衝突）仍由 BusinessExceptionFilter 處理。
    /// </summary>
    public static bool TryExecute(this Controller controller, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (BusinessRuleException ex)
        {
            controller.ModelState.AddModelError(string.Empty, ex.Message);
            return false;
        }
    }

    public static IEnumerable<SelectListItem> ToSelectList(this IEnumerable<Option> options) =>
        options.Select(o => new SelectListItem(o.Text, o.Id.ToString()));

    /// <summary>呼叫端需自行釋放回傳物件內的串流（請求結束時亦會釋放）。</summary>
    public static UploadedFile ToUpload(this IFormFile file, HttpContext context)
    {
        var stream = file.OpenReadStream();
        context.Response.RegisterForDispose(stream);
        return new UploadedFile(file.FileName, file.Length, stream);
    }
}
