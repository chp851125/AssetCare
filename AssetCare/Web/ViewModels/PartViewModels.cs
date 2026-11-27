using System.ComponentModel.DataAnnotations;
using AssetCare.Application.Dtos;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AssetCare.Web.ViewModels;

public record PartIndexViewModel(PartQuery Query, PagedResult<PartListItem> Result);

public class PartFormViewModel
{
    public Guid? Id { get; set; }
    public int Version { get; set; }
    public string? PartNo { get; set; }

    [Display(Name = "零件名稱"), Required(ErrorMessage = "請輸入零件名稱"), StringLength(100, ErrorMessage = "零件名稱過長")]
    public string? Name { get; set; }

    [Display(Name = "規格"), StringLength(100, ErrorMessage = "規格過長")]
    public string? Spec { get; set; }

    [Display(Name = "單位"), Required(ErrorMessage = "請輸入單位"), StringLength(10, ErrorMessage = "單位過長")]
    public string? Unit { get; set; }

    [Display(Name = "安全庫存"), Required(ErrorMessage = "請輸入安全庫存"), Range(0, 1_000_000, ErrorMessage = "安全庫存超出範圍")]
    public int? SafetyStock { get; set; }

    [Display(Name = "單價"), Required(ErrorMessage = "請輸入單價"), Range(0, 1_000_000_000d, ErrorMessage = "單價超出範圍")]
    public decimal? UnitCost { get; set; }

    [Display(Name = "供應廠商")]
    public Guid? VendorId { get; set; }

    [Display(Name = "存放位置"), StringLength(50, ErrorMessage = "存放位置過長")]
    public string? StorageLocation { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "期初庫存"), Range(0, 1_000_000, ErrorMessage = "期初庫存超出範圍")]
    public int InitialStock { get; set; }

    [ValidateNever]
    public IReadOnlyList<Option> Vendors { get; set; } = [];

    /// <summary>僅在 ModelState 驗證通過後呼叫。</summary>
    public PartInput ToInput() => new(Name, Spec, Unit, SafetyStock!.Value, UnitCost!.Value, VendorId,
        StorageLocation, IsActive, InitialStock);
}

public record VendorIndexViewModel(VendorQuery Query, PagedResult<VendorListItem> Result);

public class VendorFormViewModel
{
    public Guid? Id { get; set; }
    public int Version { get; set; }

    [Display(Name = "廠商名稱"), Required(ErrorMessage = "請輸入廠商名稱"), StringLength(100, ErrorMessage = "廠商名稱過長")]
    public string? Name { get; set; }

    [Display(Name = "統一編號"), RegularExpression("^[0-9]{8}$", ErrorMessage = "統一編號須為 8 碼數字")]
    public string? TaxId { get; set; }

    [Display(Name = "聯絡人"), StringLength(50, ErrorMessage = "聯絡人過長")]
    public string? ContactName { get; set; }

    [Display(Name = "電話"), StringLength(30, ErrorMessage = "電話過長")]
    public string? Phone { get; set; }

    [Display(Name = "Email"), StringLength(100, ErrorMessage = "Email 過長")]
    public string? Email { get; set; }

    [Display(Name = "地址"), StringLength(200, ErrorMessage = "地址過長")]
    public string? Address { get; set; }

    [Display(Name = "服務範圍"), StringLength(200, ErrorMessage = "服務範圍過長")]
    public string? ServiceScope { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    public VendorInput ToInput() => new(Name, TaxId, ContactName, Phone, Email, Address, ServiceScope, IsActive);
}
