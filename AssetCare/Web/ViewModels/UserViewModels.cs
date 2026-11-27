using System.ComponentModel.DataAnnotations;
using AssetCare.Application.Dtos;
using AssetCare.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AssetCare.Web.ViewModels;

public record UserIndexViewModel(UserQuery Query, PagedResult<UserListItem> Result, IReadOnlyList<Option> Departments);

/// <summary>使用者基本資料表單（新增與編輯共用欄位）。</summary>
public class UserFormViewModel
{
    public int Version { get; set; }

    [Display(Name = "姓名"), Required(ErrorMessage = "請輸入姓名"), StringLength(50, ErrorMessage = "姓名過長")]
    public string? DisplayName { get; set; }

    [Display(Name = "Email"), Required(ErrorMessage = "請輸入 Email")]
    [EmailAddress(ErrorMessage = "Email 格式不正確"), StringLength(100, ErrorMessage = "Email 過長")]
    public string? Email { get; set; }

    [Display(Name = "電話"), StringLength(30, ErrorMessage = "電話過長")]
    public string? Phone { get; set; }

    [Display(Name = "角色")]
    public UserRole Role { get; set; }

    [Display(Name = "部門")]
    public Guid? DepartmentId { get; set; }

    [ValidateNever]
    public IReadOnlyList<Option> Departments { get; set; } = [];

    public UserInput ToInput() => new(DisplayName, Email, Phone, Role, DepartmentId);
}

public class UserCreateViewModel : UserFormViewModel
{
    [Display(Name = "帳號"), Required(ErrorMessage = "請輸入帳號")]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9._-]{2,29}$", ErrorMessage = "帳號須為 3 到 30 碼英文、數字、點、底線或連字號")]
    public string? UserName { get; set; }

    [Display(Name = "初始密碼"), Required(ErrorMessage = "請輸入密碼"), DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "確認密碼"), Required(ErrorMessage = "請再次輸入密碼"), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "兩次輸入的密碼不一致")]
    public string? ConfirmPassword { get; set; }

    [ValidateNever]
    public int PasswordMinLength { get; set; }
}

public class UserEditViewModel : UserFormViewModel
{
    [ValidateNever]
    public UserEditData User { get; set; } = null!;

    [ValidateNever]
    public int PasswordMinLength { get; set; }
}

public class ResetPasswordViewModel
{
    public int Version { get; set; }

    [Display(Name = "新密碼"), Required(ErrorMessage = "請輸入新密碼"), DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Display(Name = "確認新密碼"), Required(ErrorMessage = "請再次輸入新密碼"), DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "兩次輸入的密碼不一致")]
    public string? ConfirmPassword { get; set; }
}
