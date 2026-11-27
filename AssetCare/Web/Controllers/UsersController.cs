using AssetCare.Application.Dtos;
using AssetCare.Application.Interfaces;
using AssetCare.Domain.Enums;
using AssetCare.Web.Infrastructure;
using AssetCare.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController(IUserService users) : Controller
{
    public IActionResult Index(UserQuery query)
    {
        var result = users.Search(query);
        query.Page = result.Page;
        return View(new UserIndexViewModel(query, result, users.GetDepartmentOptions()));
    }

    [HttpGet]
    public IActionResult Create() => View(Prepare(new UserCreateViewModel { Role = UserRole.Requester }));

    [HttpPost]
    public IActionResult Create(UserCreateViewModel model)
    {
        if (ModelState.IsValid &&
            this.TryExecute(() => users.Create(model.UserName, model.Password, model.ToInput())))
        {
            TempData[Flash.Success] = $"使用者 {model.DisplayName} 已新增。";
            return RedirectToAction(nameof(Index));
        }
        return View(Prepare(model));
    }

    [HttpGet]
    public IActionResult Edit(Guid id)
    {
        var data = users.GetForEdit(id);
        var i = data.Input;
        return View(Prepare(new UserEditViewModel
        {
            Version = data.Version, DisplayName = i.DisplayName, Email = i.Email, Phone = i.Phone,
            Role = i.Role, DepartmentId = i.DepartmentId
        }, data));
    }

    [HttpPost]
    public IActionResult Edit(Guid id, UserEditViewModel model)
    {
        if (ModelState.IsValid && this.TryExecute(() => users.Update(id, model.Version, model.ToInput())))
        {
            TempData[Flash.Success] = "使用者資料已更新。";
            return RedirectToAction(nameof(Index));
        }
        return View(Prepare(model, users.GetForEdit(id)));
    }

    [HttpPost]
    public IActionResult SetActive(Guid id, int version, bool isActive)
    {
        var warning = users.SetActive(id, version, isActive);
        TempData[Flash.Success] = isActive ? "帳號已啟用。" : "帳號已停用。";
        if (warning is not null) TempData[Flash.Error] = warning;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData[Flash.Error] = string.Join(" ",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Edit), new { id });
        }

        var isSelf = users.GetForEdit(id).IsSelf;
        users.ResetPassword(id, model.Version, model.NewPassword);
        if (isSelf)
        {
            await HttpContext.SignOutAsync();
            TempData[Flash.Success] = "密碼已變更，請使用新密碼重新登入。";
            return RedirectToAction("Login", "Account");
        }
        TempData[Flash.Success] = "密碼已重設，該使用者需重新登入。";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public IActionResult Unlock(Guid id, int version)
    {
        users.Unlock(id, version);
        TempData[Flash.Success] = "已解除登入鎖定。";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private UserCreateViewModel Prepare(UserCreateViewModel model)
    {
        model.Departments = users.GetDepartmentOptions();
        model.PasswordMinLength = users.GetPasswordMinLength();
        return model;
    }

    private UserEditViewModel Prepare(UserEditViewModel model, UserEditData data)
    {
        model.User = data;
        model.PasswordMinLength = users.GetPasswordMinLength();
        model.Departments = users.GetDepartmentOptions();
        // 目前所屬部門已停用時仍需出現在選單中，否則儲存會清掉原值。
        if (data.Input.DepartmentId is { } d && model.Departments.All(o => o.Id != d))
            model.Departments = [.. model.Departments, new Option(d, "（原部門，已停用）")];
        return model;
    }
}
