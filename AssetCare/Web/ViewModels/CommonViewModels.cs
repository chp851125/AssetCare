using AssetCare.Application.Dtos;

namespace AssetCare.Web.ViewModels;

public record ComingSoonViewModel(string Title, string Icon, string Message);

public record ErrorViewModel(int StatusCode, string Title, string Message, string RequestId);

public record DevToolbarViewModel(IReadOnlyList<UserOption> Users, bool CanReset);
