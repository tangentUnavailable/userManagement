using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;

namespace UserManagementApp.Features.Account.ConfirmEmail;

//     Controller for email confirmation.
public class ConfirmEmailController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public ConfirmEmailController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            TempData["ErrorMessage"] = "Invalid confirmation link.";
            return RedirectToAction("Index", "Login");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ConfirmationToken == token);

        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found. The confirmation link may have expired.";
            return RedirectToAction("Index", "Login");
        }

        if (user.Status == UserStatus.Unverified)
        {
            user.Status = UserStatus.Active;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your email has been confirmed! Your account is now active.";
        }
        else
        {
            TempData["InfoMessage"] = "Your account has already been confirmed.";
        }

        return RedirectToAction("Index", "Login", new { area = "" });
    }
}
