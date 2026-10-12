namespace BMS.API.Services;

/// <summary>
/// Abstraction over "send an email". Two implementations are registered via DI based on
/// the "Email:Provider" config value ("Dev" | "Smtp"), so switching modes needs no code
/// change - only config.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body);
}

/// <summary>
/// Default mode: does not send anything. Just logs the message so a demo/dev environment
/// works without any real email account configured. The caller (AccountsController) is
/// responsible for also returning the token/OTP directly in the API response when this
/// mode is active, so testers can complete the flow without reading server logs.
/// </summary>
public class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogInformation("[DevEmailSender] To: {ToEmail} | Subject: {Subject} | Body: {Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Real mode: sends via SMTP using settings from "Email:Smtp:*" in configuration.
/// Only used when Email:Provider = "Smtp".
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var host = _configuration["Email:Smtp:Host"];
        var portRaw = _configuration["Email:Smtp:Port"];
        var user = _configuration["Email:Smtp:User"];
        var password = _configuration["Email:Smtp:Password"];
        var from = _configuration["Email:Smtp:From"] ?? user;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(portRaw) ||
            string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException(
                "Email:Provider is set to 'Smtp' but Email:Smtp:Host/Port/User/Password/From are not fully configured in appsettings.");
        }

        if (!int.TryParse(portRaw, out var port))
        {
            throw new InvalidOperationException("Email:Smtp:Port is not a valid number.");
        }

        using var client = new System.Net.Mail.SmtpClient(host, port)
        {
            Credentials = new System.Net.NetworkCredential(user, password),
            EnableSsl = true
        };

        using var message = new System.Net.Mail.MailMessage(from, toEmail, subject, body);

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP to {ToEmail}", toEmail);
            throw;
        }
    }
}
