namespace UserManagementApp.Infrastructure.Services;

// Provides email sending capabilities, used for confirmation emails.
public interface IEmailService
{
    // email: Recipient email address.
    // name: Recipient display name.
    // confirmationLink: URL the user clicks to confirm their email.
    Task SendConfirmationEmailAsync(string email, string name, string confirmationLink);
}
