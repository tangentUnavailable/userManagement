using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Account.Login;

//     Controller for the login page.
public class LoginController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PasswordHasher<User> _passwordHasher;

    public LoginController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _passwordHasher = new PasswordHasher<User>();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl = null, string? notice = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            // A self block/delete routed us here while the session cookie was
            // still valid. If a sign-out reason was supplied, clear the now
            // invalid session and show the reason on the login page instead of
            // bouncing back to /List (which would immediately re-trigger the
            // middleware sign-out and drop the message).
            if (!string.IsNullOrEmpty(notice))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ViewData["Notice"] = notice;
                return View("Index", new LoginViewModel { ReturnUrl = "/List" });
            }

            return RedirectToAction("Index", "List");
        }

        ViewData["ReturnUrl"] = returnUrl ?? "/List";
        if (!string.IsNullOrEmpty(notice))
            ViewData["Notice"] = notice;
        return View("Index", new LoginViewModel { ReturnUrl = returnUrl ?? "/List" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            returnUrl = returnUrl ?? "/List";
            model.ReturnUrl = returnUrl;
            return View("Index", model);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower().Trim());

        if (user == null)
        {
            ModelState.AddModelError("Email", "Invalid email or password.");
            model.ReturnUrl = returnUrl ?? "/List";
            return View("Index", model);
        }

        if (user.Status == UserStatus.Blocked)
        {
            ModelState.AddModelError("Email", "Your account is blocked. Please contact the administrator.");
            model.ReturnUrl = returnUrl ?? "/List";
            return View("Index", model);
        }

        var passwordValid = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password)
            == PasswordVerificationResult.Success;

        if (!passwordValid)
        {
            ModelState.AddModelError("Email", "Invalid email or password.");
            model.ReturnUrl = returnUrl ?? "/List";
            return View("Index", model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("Status", user.Status.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        user.LastLoginTime = DateTime.UtcNow;
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(14)
                : DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal,
            authProperties);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "List");
    }
}
