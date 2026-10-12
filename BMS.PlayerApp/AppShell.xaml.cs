namespace BMS.PlayerApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Login/Register are not tabs in the TabBar (keeps the existing 3-tab layout
        // unchanged) - they are reached via a "Đăng nhập" button and pushed as detail
        // pages on top of whichever tab is active.
        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("forgot-password", typeof(ForgotPasswordPage));
        Routing.RegisterRoute("reset-password", typeof(ResetPasswordPage));
        Routing.RegisterRoute("verify-phone", typeof(VerifyPhonePage));
    }
}
