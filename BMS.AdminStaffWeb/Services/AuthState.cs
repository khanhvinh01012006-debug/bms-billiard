namespace BMS.AdminStaffWeb.Services;

/// <summary>
/// Scoped, in-memory login state for the current circuit. Not persisted across reloads
/// by design (Interactive Server keeps this alive for the lifetime of the SignalR circuit).
/// </summary>
public class AuthState
{
    public string? Token { get; private set; }
    public string? Role { get; private set; }
    public string? FullName { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool IsAdmin => IsAuthenticated && string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsStaff => IsAuthenticated && string.Equals(Role, "Staff", StringComparison.OrdinalIgnoreCase);
    public bool IsStaffOrAdmin => IsAuthenticated &&
        (string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Role, "Staff", StringComparison.OrdinalIgnoreCase));

    public event Action? Changed;

    public void SignIn(string token, string role, string fullName)
    {
        Token = token;
        Role = role;
        FullName = fullName;
        Changed?.Invoke();
    }

    public void UpdateFullName(string fullName)
    {
        FullName = fullName;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        Token = null;
        Role = null;
        FullName = null;
        Changed?.Invoke();
    }
}
