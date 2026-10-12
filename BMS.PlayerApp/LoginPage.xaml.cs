using BMS.PlayerApp.Services;

namespace BMS.PlayerApp;

public partial class LoginPage : ContentPage
{
    private readonly AccountApiService _accountApiService;
    private readonly AuthState _authState;
    private bool _showPassword;

    public LoginPage(AccountApiService accountApiService, AuthState authState)
    {
        InitializeComponent();
        _accountApiService = accountApiService;
        _authState = authState;
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        _showPassword = !_showPassword;
        PasswordEntry.IsPassword = !_showPassword;
        TogglePasswordButton.Text = _showPassword ? "Ẩn" : "Hiện";
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var email = EmailEntry.Text ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Vui lòng nhập email và mật khẩu.");
            return;
        }

        try
        {
            var (success, result, error) = await _accountApiService.LoginAsync(email, password);
            if (success && result is not null)
            {
                if (string.Equals(result.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    ShowError("Tài khoản quản trị viên vui lòng đăng nhập tại ứng dụng Quản trị (web).");
                    return;
                }

                _authState.SignIn(result.Token, result.Role, result.FullName);
                await DisplayAlert("Đăng nhập", $"Xin chào {result.FullName}!", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ShowError(error ?? "Đăng nhập thất bại.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    private async void OnGoToRegisterClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("register");
    }

    private async void OnGoToForgotPasswordClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("forgot-password");
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
