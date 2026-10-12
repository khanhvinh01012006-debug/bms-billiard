namespace BMS.API.Services;

/// <summary>
/// Abstraction over "send an OTP code via SMS". Mirrors IEmailSender. Real SMS provider
/// integration (e.g. eSMS, Twilio) is left for later once the project has a VPS/domain -
/// for now both "Dev" and "Smtp" (kept as the shared config switch name) resolve to the
/// same DevOtpSender, which never sends a real SMS.
/// </summary>
public interface IOtpSender
{
    Task SendAsync(string phoneNumber, string otpCode);
}

/// <summary>
/// Logs the OTP instead of sending a real SMS. The caller (AccountsController) also
/// returns the OTP directly in the API response when running in Dev mode, so testers can
/// complete the flow without an SMS provider account.
/// TODO: once a real SMS provider is available, add a SmsOtpSender implementation and
/// select it here the same way SmtpEmailSender is selected for email.
/// </summary>
public class DevOtpSender : IOtpSender
{
    private readonly ILogger<DevOtpSender> _logger;

    public DevOtpSender(ILogger<DevOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string otpCode)
    {
        _logger.LogInformation("[DevOtpSender] Phone: {PhoneNumber} | OTP: {OtpCode}", phoneNumber, otpCode);
        return Task.CompletedTask;
    }
}
