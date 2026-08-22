using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UserManagementApp.Infrastructure.Services;

/// <summary>
/// Email service implementation using MailKit SMTP.
/// Configuration is read from appsettings under the "Smtp" key.
/// </summary>
public class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Called from a background task, so it must not throw.
    // If sending fails, we log the error and continue — the user is still registered.
    public async Task SendConfirmationEmailAsync(string email, string name, string confirmationLink)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("User Management App", _options.From));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = "Confirm your email address";

            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2>Hello {name},</h2>
                    <p>Welcome! Please confirm your email address by clicking the button below:</p>
                    <p><a href='{confirmationLink}'
                          style='display: inline-block; padding: 10px 20px; background-color: #0d6efd;
                                 color: white; text-decoration: none; border-radius: 5px;'>
                          Confirm Email
                      </a></p>
                    <p>If you did not register, you can safely ignore this email.</p>
                    <p>&mdash; The User Management Team</p>
                </body>
                </html>";

            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.SmtpServer, _options.SmtpPort,
                MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_options.Username, _options.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Confirmation email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            // Log the error but don't rethrow — async email must not break registration
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", email);
        }
    }
}

/// <summary>
/// Strongly-typed configuration for SMTP settings. Bound from appsettings.json under "Smtp".
/// </summary>
public class SmtpOptions
{
    public string SmtpServer { get; set; } = null!;
    public int SmtpPort { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string From { get; set; } = null!;
}
