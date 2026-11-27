using System.ComponentModel.DataAnnotations;
using AssetCare.Application.Dtos;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AssetCare.Web.ViewModels;

public class CategoryFormViewModel
{
    public Guid? Id { get; set; }
    public int Version { get; set; }
    public bool CodeLocked { get; set; }

    [Display(Name = "類別代碼"), Required(ErrorMessage = "請輸入類別代碼")]
    [RegularExpression("^[A-Za-z0-9]{2,6}$", ErrorMessage = "類別代碼須為 2 到 6 碼英文或數字")]
    public string? Code { get; set; }

    [Display(Name = "類別名稱"), Required(ErrorMessage = "請輸入類別名稱"), StringLength(50, ErrorMessage = "類別名稱過長")]
    public string? Name { get; set; }

    [Display(Name = "說明"), StringLength(200, ErrorMessage = "說明過長")]
    public string? Description { get; set; }

    [Display(Name = "預設耐用年限（年）"), Range(1, 100, ErrorMessage = "預設耐用年限須為 1 到 100 年")]
    public int DefaultUsefulLifeYears { get; set; } = 5;

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    public CategoryInput ToInput() => new(Code, Name, Description, DefaultUsefulLifeYears, IsActive);
}

public class LocationFormViewModel
{
    public Guid? Id { get; set; }
    public int Version { get; set; }

    [Display(Name = "地點代碼"), Required(ErrorMessage = "請輸入地點代碼")]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9-]{0,19}$", ErrorMessage = "地點代碼僅能使用英文、數字與連字號（最多 20 碼）")]
    public string? Code { get; set; }

    [Display(Name = "地點名稱"), Required(ErrorMessage = "請輸入地點名稱"), StringLength(50, ErrorMessage = "地點名稱過長")]
    public string? Name { get; set; }

    [Display(Name = "上層地點")]
    public Guid? ParentId { get; set; }

    [Display(Name = "排序值"), Range(0, 100000, ErrorMessage = "排序值須為 0 到 100000")]
    public int SortOrder { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    [ValidateNever]
    public IReadOnlyList<Option> ParentOptions { get; set; } = [];

    public LocationInput ToInput() => new(Code, Name, ParentId, SortOrder, IsActive);
}
