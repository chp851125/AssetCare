using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

/// <summary>
/// 清單、詳情與行事曆限員工；維護與手動產生限主管以上；
/// 使用量登錄限主管或該計畫的負責技術員（由 Service 檢查）。
/// </summary>
[Authorize(Roles = Roles.Staff)]
public class MaintenancePlansController(IMaintenancePlanService plans, ITimeProvider time) : Controller
{
    [HttpGet]
    public IActionResult Index(PlanQuery query)
    {
        var result = plans.Search(query);
        query.Page = result.Page;
        return View(new PlanIndexViewModel(query, result));
    }

    [HttpGet]
    public IActionResult Details(Guid id) => View(plans.GetDetail(id));

    [HttpGet]
    public IActionResult Calendar() => View();

    /// <summary>行事曆資料；start、end 由 FullCalendar 帶入（可能含時間，只取日期）。</summary>
    [HttpGet]
    public IActionResult Events(string? start, string? end) =>
        TryDate(start, out var from) && TryDate(end, out var to)
            ? Json(plans.GetCalendar(from, to))
            : BadRequest();

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Create(Guid? assetId)
    {
        var options = plans.GetFormOptions();
        return View("Form", new PlanFormViewModel
        {
            Options = options, AssetId = assetId, LeadDays = options.DefaultLeadDays, NextDueDate = time.Today.AddMonths(1)
        });
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Create(PlanFormViewModel form)
    {
        form.Id = null;
        var id = Guid.Empty;
        if (ModelState.IsValid && this.TryExecute(() => id = plans.Create(form.ToInput())))
        {
            TempData[Flash.Success] = "保養計畫已新增。";
            return RedirectToAction(nameof(Details), new { id });
        }
        form.Options = plans.GetFormOptions();
        return View("Form", form);
    }

    [Authorize(Roles = Roles.Managers), HttpGet]
    public IActionResult Edit(Guid id)
    {
        var data = plans.GetForEdit(id);
        var i = data.Input;
        return View("Form", new PlanFormViewModel
        {
            Id = data.Id, Version = data.Version, AssetLabel = data.AssetLabel, AssetId = i.AssetId, Name = i.Name,
            Cycle = i.Cycle, Priority = i.Priority, NextDueDate = i.NextDueDate, LeadDays = i.LeadDays,
            UsageInterval = i.UsageInterval, NextDueUsage = i.NextDueUsage, AssigneeUserId = i.AssigneeUserId,
            Checklist = i.Checklist, IsActive = i.IsActive, Options = plans.GetFormOptions()
        });
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Edit(Guid id, PlanFormViewModel form)
    {
        if (ModelState.IsValid && this.TryExecute(() => plans.Update(id, form.Version, form.ToInput())))
        {
            TempData[Flash.Success] = "保養計畫已更新。";
            return RedirectToAction(nameof(Details), new { id });
        }
        var current = plans.GetForEdit(id);
        form.Id = id;
        form.AssetLabel = current.AssetLabel;
        form.Options = plans.GetFormOptions();
        return View("Form", form);
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult SetActive(Guid id, int version, bool isActive)
    {
        plans.SetActive(id, version, isActive);
        TempData[Flash.Success] = isActive ? "保養計畫已啟用。" : "保養計畫已停用。";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public IActionResult RecordUsage(Guid id, int assetVersion, decimal? usage)
    {
        if (usage is not { } value)
        {
            TempData[Flash.Error] = "請填寫目前累計使用量。";
        }
        else
        {
            var count = plans.RecordUsage(id, assetVersion, value);
            TempData[Flash.Success] = count > 0 ? $"使用量已登錄，並產生 {count} 張保養工單。" : "使用量已登錄。";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Managers), HttpPost]
    public IActionResult Generate()
    {
        var count = plans.GenerateNow();
        TempData[Flash.Success] = count > 0 ? $"已產生 {count} 張保養工單。" : "目前沒有需要產生的保養工單。";
        return RedirectToAction(nameof(Index));
    }

    private static bool TryDate(string? value, out DateOnly date)
    {
        date = default;
        return value is { Length: >= 10 } && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", out date);
    }
}
