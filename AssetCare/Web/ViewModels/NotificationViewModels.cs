using AssetCare.Application.Dtos;

namespace AssetCare.Web.ViewModels;

public record NotificationIndexViewModel(NotificationQuery Query, PagedResult<NotificationItem> Result,
    int UnreadCount);
