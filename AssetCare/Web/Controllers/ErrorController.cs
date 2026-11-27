using System.Diagnostics;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorController : Controller
{
    [Route("/Error/{code:int?}")]
    public IActionResult Index([FromRoute] int? code)
    {
        var status = code ?? StatusCodes.Status500InternalServerError;
        var (title, message) = status switch
        {
            400 => ("要求無法處理", "表單可能已過期，請回上一頁重新整理後再試一次。"),
            403 => ("沒有權限", "您沒有權限執行此操作。"),
            404 => ("找不到頁面", "您要找的頁面或資料不存在，可能已被移除。"),
            _ => ("系統發生錯誤", "很抱歉，系統暫時無法完成您的要求。請稍後再試，或將下方追蹤碼提供給系統管理員。")
        };
        Response.StatusCode = status;
        return View(new ErrorViewModel(status, title, message, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }
}
