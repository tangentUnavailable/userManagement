using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp;

/// <summary>
/// Barebones home controller. Just redirects to login or the user list,
/// and renders the error page.
/// </summary>
public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "List");

        return RedirectToAction("Index", "Login");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var model = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
        return View(model);
    }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
