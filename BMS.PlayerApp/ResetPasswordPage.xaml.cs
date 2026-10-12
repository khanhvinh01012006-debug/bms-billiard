using BMS.PlayerApp.Services;

namespace BMS.PlayerApp;

public partial class ResetPasswordPage : ContentPage
{
    private readonly AccountApiService _accountApiService;
    private bool _showPassword;

    public ResetPasswordPage(AccountApiService accountApiService)
    {
        InitializeComponent();
        _accountApiService = accountApiService;
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        _showPassword = !_showPassword;
        NewPasswordEntry.IsPassword = !_showPassword;
        TogglePasswordButton.Text = _showPassword ? "Ẩn" : "Hiện";
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var email = EmailEntry.Text ?? string.Empty;
        var token = TokenEntry.Text ?? string.Empty;
        var newPassword = NewPasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
        {
            ShowError("Vui lòng nhập đầy đủ email, mã và mật khẩu mới.");
            return;
        }

        var (success, error) = await _accountApiService.ResetPasswordAsync(email, token, newPassword);
        if (success)
        {
            await DisplayAlert("Thành công", "Đã đặt lại mật khẩu. Vui lòng đăng nhập lại.", "OK");
            await Shell.Current.GoToAsync("login");
        }
        else
        {
            ShowError(error ?? "Đặt lại mật khẩu thất bại.");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
