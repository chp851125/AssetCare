using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class SettingsController(ISettingService settings) : Controller
{
    [HttpGet]
    public IActionResult Index() =>
        View(new SettingsViewModel(settings.GetAll(), new Dictionary<Guid, string?>()));

    [HttpPost]
    public IActionResult Index(List<SettingChangeViewModel> items)
    {
        var count = 0;
        if (this.TryExecute(() => count = settings.Update(
                items.Select(i => new SettingChange(i.Id, i.Version, i.Value)).ToList())))
        {
            TempData[Flash.Success] = count > 0 ? $"已更新 {count} 項參數。" : "沒有變更任何參數。";
            return RedirectToAction(nameof(Index));
        }
        var posted = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First().Value);
        return View(new SettingsViewModel(settings.GetAll(), posted));
    }
}
