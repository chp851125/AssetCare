using System.ComponentModel.DataAnnotations;

namespace AssetCare.Web.ViewModels;

public class LoginViewModel
{
    [Display(Name = "帳號")]
    [Required(ErrorMessage = "請輸入帳號")]
    [StringLength(50, ErrorMessage = "帳號過長")]
    public string UserName { get; set; } = "";

    [Display(Name = "密碼")]
    [Required(ErrorMessage = "請輸入密碼")]
    [StringLength(100, ErrorMessage = "密碼過長")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
