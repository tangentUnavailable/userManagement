using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Infrastructure.Data;
using UserManagementApp.Infrastructure.Middleware;
using UserManagementApp.Infrastructure.Services;

Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");

var builder = WebApplication.CreateBuilder(args);

// Listen on the PORT env var if set, otherwise fall back to port 80.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
else
{
    builder.WebHost.UseUrls("http://0.0.0.0:80");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.SlidingExpiration = true;
    });

builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedBackgroundService>();

builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Dev gets detailed errors and HTTPS. Production serves plain HTTP on port 80.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseUserStatus();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Make sure migrations are applied and seed an admin user on first run.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // The database might not be ready yet in container environments.
        var retryCount = 0;
        var maxRetries = 15;
        while (true)
        {
            try
            {
                context.Database.Migrate();
                break;
            }
            catch (Exception dbEx) when (retryCount < maxRetries)
            {
                retryCount++;
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(
                    "Database connection failed (attempt {RetryCount}/{MaxRetries}). Retrying in 2 seconds... Error: {Message}",
                    retryCount, maxRetries, dbEx.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        // Seed a default admin if the table is empty.
        if (!await context.Users.AnyAsync())
        {
            var passwordHasher = new PasswordHasher<User>();
            var admin = new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "System Administrator",
                Email = "admin@example.com",
                PasswordHash = passwordHasher.HashPassword(null!, "admin"),
                Status = UserStatus.Active,
                RegistrationTime = DateTime.UtcNow,
                ConfirmationToken = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();
