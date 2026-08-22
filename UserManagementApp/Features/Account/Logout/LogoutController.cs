using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace UserManagementApp.Features.Account.Logout;

/// <summary>
/// Signs the current user out of the cookie auth scheme.
/// </summary>
public class LogoutController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "You have been logged out.";
        return RedirectToAction("Index", "Login");
    }
}
