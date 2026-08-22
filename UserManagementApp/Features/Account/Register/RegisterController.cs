using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Extensions;
using UserManagementApp.Infrastructure.Services;

namespace UserManagementApp.Features.Account.Register;

/// <summary>
/// Controller for user registration.
/// The register endpoint is EXEMPT from UserStatusMiddleware (no user is logged in yet).
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

    // GET: Display the registration form.
    [HttpGet]
    public IActionResult Index(string? returnUrl = null)
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "List");

        ViewData["ReturnUrl"] = returnUrl ?? "/Login";
        return View("Index", new RegisterViewModel { ReturnUrl = returnUrl ?? "/Login" });
    }

    // POST: Handle registration form submission.
    // Email uniqueness is enforced by the database UNIQUE INDEX,
    // and also checked at the application layer for a better error message.
    // Any non-empty password is accepted. Users can log in even when unverified.
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

        // Normalize email (trim + lowercase) to prevent case-variant duplicates
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // Pre-check: look up existing email for a better UX error message.
        // The UNIQUE INDEX is the ultimate guarantee; this just provides fast feedback.
        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "This email address is already registered.");
            return View("Index", model);
        }

        // Create the user with "Unverified" status
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            Status = UserStatus.Unverified,
            RegistrationTime = DateTime.UtcNow,
            LastLoginTime = null,
            // ConfirmationToken is used in the email confirmation link
            ConfirmationToken = Guid.NewGuid().ToString(),
        };

        // Hash the password using PBKDF2
        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _dbContext.Users.Add(user);

        try
        {
            // Save to database. The UNIQUE INDEX throws DbUpdateException on
            // race-condition duplicate inserts — handled in the catch below.
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: another request inserted the same email between
            // the pre-check and SaveChanges. The UNIQUE INDEX caught it here.
            ModelState.AddModelError("Email", "This email address is already registered.");
            return View("Index", model);
        }

        // Queue the confirmation email for async sending
        var baseUrl = $"{_httpContextAccessor.HttpContext!.Request.Scheme}://" +
                      $"{_httpContextAccessor.HttpContext.Request.Host}";
        var confirmationLink = $"{baseUrl}/ConfirmEmail?token={user.ConfirmationToken}";

        _taskQueue.QueueBackgroundWorkItem(async (cancellationToken) =>
        {
            // Use IServiceScopeFactory (singleton) to create a fresh DI scope
            // so the background task can resolve IEmailService without a
            // request-scoped IServiceProvider that would be disposed.
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await emailService.SendConfirmationEmailAsync(user.Email, user.Name, confirmationLink);
        });

        // Show success message and redirect to login
        TempData["SuccessMessage"] = "Registration successful! A confirmation email has been sent. " +
                                     "You can log in now with unverified status.";

        return RedirectToAction("Index", "Login");
    }

    // Helper: checks if a DbUpdateException is caused by a unique constraint violation.
    // PostgreSQL throws PostgresException with SQLSTATE 23505 (unique_violation).
    // See: https://www.postgresql.org/docs/current/errcodes-appendix.html
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == "23505";
        }
        return false;
    }
}
