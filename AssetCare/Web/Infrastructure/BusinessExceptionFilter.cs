using AssetCare.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AssetCare.Web.Infrastructure;

/// <summary>把 Service 丟出的商業例外轉成友善回應。</summary>
public class BusinessExceptionFilter(ITempDataDictionaryFactory tempDataFactory, ILogger<BusinessExceptionFilter> logger)
    : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ForbiddenException:
                logger.LogWarning("拒絕存取 {Path}：{Message}", context.HttpContext.Request.Path, context.Exception.Message);
                context.Result = new ForbidResult();
                break;
            case NotFoundException:
                context.Result = new NotFoundResult();
                break;
            case BusinessRuleException or ConcurrencyException:
                logger.LogInformation("操作被拒絕 {Path}：{Message}", context.HttpContext.Request.Path, context.Exception.Message);
                tempDataFactory.GetTempData(context.HttpContext)[Flash.Error] = context.Exception.Message;
                context.Result = new LocalRedirectResult(ReturnPath(context.HttpContext.Request));
                break;
            default:
                return;
        }
        context.ExceptionHandled = true;
    }

    /// <summary>POST 失敗時回到來源頁；其他情況回首頁，避免重導迴圈。</summary>
    private static string ReturnPath(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) &&
            Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) &&
            string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase) &&
            !referer.PathAndQuery.StartsWith("//"))
            return referer.PathAndQuery;
        return request.PathBase + "/";
    }
}
