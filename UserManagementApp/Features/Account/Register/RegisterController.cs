using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;
using UserManagementApp.Infrastructure.Services;

namespace UserManagementApp.Features.Account.Register;

/// <summary>
/// Registration endpoint. Exempt from UserStatusMiddleware since no one
/// is signed in yet.
/// </summary>
public class RegisterController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PasswordHasher<User> _passwordHasher;

    public RegisterController(
        ApplicationDbContext dbContext,
        IBackgroundTaskQueue taskQueue,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _taskQueue = taskQueue;
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
        _passwordHasher = new PasswordHasher<User>();
    }

    [HttpGet]
    public IActionResult Index(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "List");

        ViewData["ReturnUrl"] = returnUrl ?? "/Login";
        return View("Index", new RegisterViewModel { ReturnUrl = returnUrl ?? "/Login" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RegisterViewModel model, string? returnUrl = null)
    {
        returnUrl = returnUrl ?? "/Login";
        model.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // Pre-check so we can show a friendly error instead of a DB exception.
        // The unique index is still the real backstop.
        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "This email address is already registered.");
            return View("Index", model);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            Status = UserStatus.Unverified,
            RegistrationTime = DateTime.UtcNow,
            LastLoginTime = null,
            ConfirmationToken = Guid.NewGuid().ToString(),
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Lost a race with another registration request. The index caught it.
            ModelState.AddModelError("Email", "This email address is already registered.");
            return View("Index", model);
        }

        var baseUrl = $"{_httpContextAccessor.HttpContext!.Request.Scheme}://" +
                      $"{_httpContextAccessor.HttpContext.Request.Host}";
        var confirmationLink = $"{baseUrl}/ConfirmEmail?token={user.ConfirmationToken}";

        _taskQueue.QueueBackgroundWorkItem(async (cancellationToken) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await emailService.SendConfirmationEmailAsync(user.Email, user.Name, confirmationLink);
        });

        TempData["SuccessMessage"] = "Registration successful! A confirmation email has been sent. " +
                                     "You can log in now with unverified status.";

        return RedirectToAction("Index", "Login");
    }

    // PostgreSQL throws 23505 on unique violations.
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == "23505";
        }
        return false;
    }
}
