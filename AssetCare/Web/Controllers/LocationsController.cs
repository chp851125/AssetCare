using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Managers)]
public class LocationsController(ILocationService locations) : Controller
{
    public IActionResult Index() => View(locations.GetTree());

    [HttpGet]
    public IActionResult Create(Guid? parentId) => View("Form",
        new LocationFormViewModel { ParentId = parentId, ParentOptions = locations.GetParentOptions(null) });

    [HttpPost]
    public IActionResult Create(LocationFormViewModel model)
    {
        model.Id = null;
        if (ModelState.IsValid && this.TryExecute(() => locations.Create(model.ToInput())))
        {
            TempData[Flash.Success] = "地點已新增。";
            return RedirectToAction(nameof(Index));
        }
        model.ParentOptions = locations.GetParentOptions(null);
        return View("Form", model);
    }

    [HttpGet]
    public IActionResult Edit(Guid id)
    {
        var data = locations.GetForEdit(id);
        var i = data.Input;
        return View("Form", new LocationFormViewModel
        {
            Id = data.Id, Version = data.Version, Code = i.Code, Name = i.Name, ParentId = i.ParentId,
            SortOrder = i.SortOrder, IsActive = i.IsActive, ParentOptions = locations.GetParentOptions(id)
        });
    }

    [HttpPost]
    public IActionResult Edit(Guid id, LocationFormViewModel model)
    {
        if (ModelState.IsValid && this.TryExecute(() => locations.Update(id, model.Version, model.ToInput())))
        {
            TempData[Flash.Success] = "地點已更新。";
            return RedirectToAction(nameof(Index));
        }
        model.Id = id;
        model.ParentOptions = locations.GetParentOptions(id);
        return View("Form", model);
    }

    [HttpPost]
    public IActionResult Delete(Guid id, int version)
    {
        locations.Delete(id, version);
        TempData[Flash.Success] = "地點已刪除。";
        return RedirectToAction(nameof(Index));
    }
}
