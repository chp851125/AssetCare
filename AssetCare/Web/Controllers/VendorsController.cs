using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Managers)]
public class VendorsController(IVendorService vendors) : Controller
{
    [HttpGet]
    public IActionResult Index(VendorQuery query)
    {
        var result = vendors.Search(query);
        query.Page = result.Page;
        return View(new VendorIndexViewModel(query, result));
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new VendorFormViewModel());

    [HttpPost]
    public IActionResult Create(VendorFormViewModel form)
    {
        form.Id = null;
        if (ModelState.IsValid && this.TryExecute(() => vendors.Create(form.ToInput())))
        {
            TempData[Flash.Success] = "廠商已新增。";
            return RedirectToAction(nameof(Index));
        }
        return View("Form", form);
    }

    [HttpGet]
    public IActionResult Edit(Guid id)
    {
        var data = vendors.GetForEdit(id);
        var i = data.Input;
        return View("Form", new VendorFormViewModel
        {
            Id = data.Id, Version = data.Version, Name = i.Name, TaxId = i.TaxId, ContactName = i.ContactName,
            Phone = i.Phone, Email = i.Email, Address = i.Address, ServiceScope = i.ServiceScope,
            IsActive = i.IsActive
        });
    }

    [HttpPost]
    public IActionResult Edit(Guid id, VendorFormViewModel form)
    {
        if (ModelState.IsValid && this.TryExecute(() => vendors.Update(id, form.Version, form.ToInput())))
        {
            TempData[Flash.Success] = "廠商資料已更新。";
            return RedirectToAction(nameof(Index));
        }
        form.Id = id;
        return View("Form", form);
    }
}
