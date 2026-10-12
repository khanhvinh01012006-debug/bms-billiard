namespace BMS.PlayerWeb.Services;

/// <summary>
/// Scoped, in-memory login state for the current WASM session. Simpler than
/// BMS.AdminStaffWeb.Services.AuthState - players only need to know whether they are
/// signed in and what name/token to use, not role-based admin/staff checks.
/// </summary>
public class AuthState
{
    public string? Token { get; private set; }
    public string? Role { get; private set; }
    public string? FullName { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool IsPlayer => IsAuthenticated && string.Equals(Role, "Player", StringComparison.OrdinalIgnoreCase);

    public event Action? Changed;

    public void SignIn(string token, string role, string fullName)
    {
        Token = token;
        Role = role;
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

    public void UpdateFullName(string fullName)
    {
        FullName = fullName;
        Changed?.Invoke();
    }
}
