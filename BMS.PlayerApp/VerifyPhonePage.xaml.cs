using BMS.PlayerApp.Services;

namespace BMS.PlayerApp;

public partial class VerifyPhonePage : ContentPage
{
    private readonly AccountApiService _accountApiService;

    public VerifyPhonePage(AccountApiService accountApiService)
    {
        InitializeComponent();
        _accountApiService = accountApiService;
    }

    private async void OnSendOtpClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        OtpInfoLabel.IsVisible = false;

        var phone = PhoneEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            ShowError("Vui lòng nhập số điện thoại.");
            return;
        }

        var (success, result, error) = await _accountApiService.SendPhoneOtpAsync(phone);
        if (success && result is not null)
        {
            // Chế độ Dev: API trả kèm DebugOtp để demo không cần dịch vụ SMS thật.
            OtpInfoLabel.Text = string.IsNullOrEmpty(result.DebugOtp)
                ? result.Message
                : $"{result.Message}\nMã (chế độ demo): {result.DebugOtp}";
            OtpInfoLabel.IsVisible = true;
        }
        else
        {
            ShowError(error ?? "Gửi mã OTP thất bại.");
        }
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        SuccessLabel.IsVisible = false;

        var otp = OtpEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(otp))
        {
            ShowError("Vui lòng nhập mã OTP.");
            return;
        }

        var (success, error) = await _accountApiService.VerifyPhoneOtpAsync(otp);
        if (success)
        {
            SuccessLabel.IsVisible = true;
        }
        else
        {
            ShowError(error ?? "Xác thực OTP thất bại.");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
