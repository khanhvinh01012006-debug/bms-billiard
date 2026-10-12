using BMS.PlayerApp.Services;

namespace BMS.PlayerApp;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly AccountApiService _accountApiService;

    public ForgotPasswordPage(AccountApiService accountApiService)
    {
        InitializeComponent();
        _accountApiService = accountApiService;
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        InfoLabel.IsVisible = false;

        var email = EmailEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Vui lòng nhập email.");
            return;
        }

        var (success, result, error) = await _accountApiService.ForgotPasswordAsync(email);
        if (success && result is not null)
        {
            // Chế độ Dev: API trả kèm DebugToken để demo không cần dịch vụ email thật.
            InfoLabel.Text = string.IsNullOrEmpty(result.DebugToken)
                ? result.Message
                : $"{result.Message}\nMã (chế độ demo): {result.DebugToken}";
            InfoLabel.IsVisible = true;
        }
        else
        {
            ShowError(error ?? "Yêu cầu thất bại.");
        }
    }

    private async void OnGoToResetClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("reset-password");
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
