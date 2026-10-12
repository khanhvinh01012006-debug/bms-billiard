using BMS.PlayerApp.Services;

namespace BMS.PlayerApp;

public partial class RegisterPage : ContentPage
{
    private readonly AccountApiService _accountApiService;
    private readonly AuthState _authState;
    private bool _showPassword;

    public RegisterPage(AccountApiService accountApiService, AuthState authState)
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

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var fullName = FullNameEntry.Text ?? string.Empty;
        var email = EmailEntry.Text ?? string.Empty;
        var phone = PhoneEntry.Text;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Vui lòng nhập đầy đủ họ tên, email và mật khẩu.");
            return;
        }

        try
        {
            var (success, result, error) = await _accountApiService.RegisterAsync(fullName, email, password, phone);
            if (success && result is not null)
            {
                _authState.SignIn(result.Token, result.Role, result.FullName);
                await DisplayAlert("Đăng ký", $"Chào mừng {result.FullName}!", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ShowError(error ?? "Đăng ký thất bại.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
