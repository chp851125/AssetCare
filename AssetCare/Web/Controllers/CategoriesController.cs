using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Managers)]
public class CategoriesController(ICategoryService categories) : Controller
{
    public IActionResult Index() => View(categories.GetList());

    [HttpGet]
    public IActionResult Create() => View("Form", new CategoryFormViewModel());

    [HttpPost]
    public IActionResult Create(CategoryFormViewModel model)
    {
        model.Id = null;
        model.CodeLocked = false;
        if (ModelState.IsValid && this.TryExecute(() => categories.Create(model.ToInput())))
        {
            TempData[Flash.Success] = "資產類別已新增。";
            return RedirectToAction(nameof(Index));
        }
        return View("Form", model);
    }

    [HttpGet]
    public IActionResult Edit(Guid id)
    {
        var data = categories.GetForEdit(id);
        var i = data.Input;
        return View("Form", new CategoryFormViewModel
        {
            Id = data.Id, Version = data.Version, CodeLocked = data.CodeLocked, Code = i.Code, Name = i.Name,
            Description = i.Description, DefaultUsefulLifeYears = i.DefaultUsefulLifeYears, IsActive = i.IsActive
        });
    }

    [HttpPost]
    public IActionResult Edit(Guid id, CategoryFormViewModel model)
    {
        if (ModelState.IsValid && this.TryExecute(() => categories.Update(id, model.Version, model.ToInput())))
        {
            TempData[Flash.Success] = "資產類別已更新。";
            return RedirectToAction(nameof(Index));
        }
        model.Id = id;
        model.CodeLocked = categories.GetForEdit(id).CodeLocked;
        return View("Form", model);
    }

    [HttpPost]
    public IActionResult Delete(Guid id, int version)
    {
        categories.Delete(id, version);
        TempData[Flash.Success] = "資產類別已刪除。";
        return RedirectToAction(nameof(Index));
    }
}
