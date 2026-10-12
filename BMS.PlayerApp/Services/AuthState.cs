namespace BMS.PlayerApp.Services;

/// <summary>
/// Simple, in-memory login state for the app's lifetime (mirrors BMS.PlayerWeb's AuthState).
/// Not persisted to disk yet - the user signs in again after the app is fully closed.
/// </summary>
public class AuthState
{
    public string? Token { get; private set; }
    public string? Role { get; private set; }
    public string? FullName { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool IsStaff => IsAuthenticated && string.Equals(Role, "Staff", StringComparison.OrdinalIgnoreCase);
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
}
