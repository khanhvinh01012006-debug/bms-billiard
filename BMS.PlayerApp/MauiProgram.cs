using BMS.PlayerApp.Services;
using Microsoft.Extensions.Logging;

namespace BMS.PlayerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // Đợt 2: font thật đã có trong Resources/Fonts (MauiFont trong csproj):
            // Inter = font chữ của thiết kế Stitch tinh gọn, MaterialIcons = icon dạng font.
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter.ttf", "Inter");
                fonts.AddFont("MaterialIconsOutlined.otf", "MaterialIcons");
            });

        builder.Services.AddSingleton(new ApiOptions());
        builder.Services.AddSingleton<AuthState>();
        builder.Services.AddSingleton<AccountApiService>();
        builder.Services.AddSingleton<TableApiService>();
        builder.Services.AddSingleton<CueApiService>();
        builder.Services.AddSingleton<PlayerApiService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<CuesPage>();
        builder.Services.AddTransient<MatchesPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<VerifyPhonePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
