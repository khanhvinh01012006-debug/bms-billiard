namespace BMS.PlayerApp.Services;

/// <summary>
/// API base URL for the Android emulator. The Android emulator cannot resolve "localhost"
/// as the host machine - 10.0.2.2 is the special alias the emulator provides for that.
/// On a physical device on the same network, replace this with the host machine's LAN IP.
/// </summary>
public class ApiOptions
{
    public string BaseUrl { get; set; } = "https://10.0.2.2:5001";
}
