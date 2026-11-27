using System.ComponentModel.DataAnnotations;
using AssetCare.Application.Dtos;
using AssetCare.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AssetCare.Web.ViewModels;

public record AssetIndexViewModel(AssetQuery Query, PagedResult<AssetListItem> Result, AssetFilterOptions Options);

public class AssetFormViewModel
{
    public Guid? Id { get; set; }
    public int Version { get; set; }
    public string? AssetNo { get; set; }
    /// <summary>維修中的資產狀態由工單控制，不可手動變更。</summary>
    public bool StatusLocked { get; set; }

    [Display(Name = "資產名稱"), Required(ErrorMessage = "請輸入資產名稱"), StringLength(100, ErrorMessage = "資產名稱過長")]
    public string? Name { get; set; }

    [Display(Name = "資產類別"), Required(ErrorMessage = "請選擇資產類別")]
    public Guid? CategoryId { get; set; }

    [Display(Name = "狀態")]
    public AssetStatus Status { get; set; }

    [Display(Name = "廠牌"), StringLength(50, ErrorMessage = "廠牌過長")]
    public string? Brand { get; set; }

    [Display(Name = "型號"), StringLength(50, ErrorMessage = "型號過長")]
    public string? Model { get; set; }

    [Display(Name = "序號"), StringLength(80, ErrorMessage = "序號過長")]
    public string? SerialNo { get; set; }

    [Display(Name = "購入日期"), Required(ErrorMessage = "請輸入購入日期")]
    public DateOnly? PurchaseDate { get; set; }

    [Display(Name = "購入成本"), Required(ErrorMessage = "請輸入購入成本")]
    [Range(0, 1_000_000_000_000d, ErrorMessage = "購入成本超出範圍")]
    public decimal? PurchaseCost { get; set; }

    [Display(Name = "殘值"), Required(ErrorMessage = "請輸入殘值")]
    [Range(0, 1_000_000_000_000d, ErrorMessage = "殘值超出範圍")]
    public decimal? SalvageValue { get; set; }

    [Display(Name = "耐用年限（年）"), Required(ErrorMessage = "請輸入耐用年限")]
    [Range(1, 100, ErrorMessage = "耐用年限須為 1 到 100 年")]
    public int? UsefulLifeYears { get; set; }

    [Display(Name = "保固到期日")]
    public DateOnly? WarrantyEndDate { get; set; }

    [Display(Name = "地點"), Required(ErrorMessage = "請選擇地點")]
    public Guid? LocationId { get; set; }

    [Display(Name = "部門")]
    public Guid? DepartmentId { get; set; }

    [Display(Name = "保管人")]
    public Guid? CustodianUserId { get; set; }

    [Display(Name = "供應／維修廠商")]
    public Guid? VendorId { get; set; }

    [Display(Name = "說明"), StringLength(1000, ErrorMessage = "說明過長")]
    public string? Description { get; set; }

    [Display(Name = "使用量單位"), StringLength(20, ErrorMessage = "使用量單位過長")]
    public string? UsageUnit { get; set; }

    [Display(Name = "目前使用量"), Range(0, 1_000_000_000_000d, ErrorMessage = "目前使用量超出範圍")]
    public decimal? CurrentUsage { get; set; }

    [ValidateNever]
    public AssetFormOptions Options { get; set; } = null!;

    /// <summary>僅在 ModelState 驗證通過後呼叫。</summary>
    public AssetInput ToInput() => new(Name, CategoryId!.Value, Status, Brand, Model, SerialNo, PurchaseDate!.Value,
        PurchaseCost!.Value, SalvageValue!.Value, UsefulLifeYears!.Value, WarrantyEndDate, LocationId!.Value,
        DepartmentId, CustodianUserId, VendorId, Description, UsageUnit, CurrentUsage);

    public static AssetFormViewModel From(AssetEditData data)
    {
        var i = data.Input;
        return new AssetFormViewModel
        {
            Id = data.Id, Version = data.Version, AssetNo = data.AssetNo,
            StatusLocked = i.Status == AssetStatus.UnderRepair,
            Name = i.Name, CategoryId = i.CategoryId, Status = i.Status, Brand = i.Brand, Model = i.Model,
            SerialNo = i.SerialNo, PurchaseDate = i.PurchaseDate, PurchaseCost = i.PurchaseCost,
            SalvageValue = i.SalvageValue, UsefulLifeYears = i.UsefulLifeYears, WarrantyEndDate = i.WarrantyEndDate,
            LocationId = i.LocationId, DepartmentId = i.DepartmentId, CustodianUserId = i.CustodianUserId,
            VendorId = i.VendorId, Description = i.Description, UsageUnit = i.UsageUnit, CurrentUsage = i.CurrentUsage
        };
    }
}

public class AssetTransferViewModel
{
    public int Version { get; set; }

    [Display(Name = "新地點"), Required(ErrorMessage = "請選擇地點")]
    public Guid? LocationId { get; set; }

    [Display(Name = "新部門")]
    public Guid? DepartmentId { get; set; }

    [Display(Name = "新保管人")]
    public Guid? CustodianUserId { get; set; }

    [Display(Name = "轉移原因"), Required(ErrorMessage = "請輸入轉移原因"), StringLength(500, ErrorMessage = "轉移原因過長")]
    public string? Reason { get; set; }

    [ValidateNever]
    public AssetSummary Asset { get; set; } = null!;

    [ValidateNever]
    public AssetFormOptions Options { get; set; } = null!;
}

public class AssetScrapViewModel
{
    public int Version { get; set; }

    [Display(Name = "報廢原因"), Required(ErrorMessage = "請輸入報廢原因"), StringLength(500, ErrorMessage = "報廢原因過長")]
    public string? Reason { get; set; }

    [ValidateNever]
    public AssetSummary Asset { get; set; } = null!;
}

public record AssetImportViewModel(ImportResult? Result);

public record AssetLabelsViewModel(IReadOnlyList<AssetLabel> Labels, bool Large, bool IsLocalAddress);
