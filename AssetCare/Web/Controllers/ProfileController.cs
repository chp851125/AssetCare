using AssetCare.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssetCare.Web.Controllers;

public class ProfileController(IUserService users) : Controller
{
    public IActionResult Index() => View(users.GetCurrentProfile());
}
