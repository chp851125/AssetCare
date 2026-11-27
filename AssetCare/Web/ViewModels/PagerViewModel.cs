using AssetCare.Application.Dtos;

namespace AssetCare.Web.ViewModels;

public record PagerViewModel(int Page, int TotalPages, int Total)
{
    public static PagerViewModel From<T>(PagedResult<T> result) => new(result.Page, result.TotalPages, result.Total);
}
