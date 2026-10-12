using System.Text.Json;
using Microsoft.JSInterop;

namespace BMS.PlayerWeb.Services;

/// <summary>
/// Persists the login session in the browser (localStorage or sessionStorage) so a page
/// reload (F5) doesn't silently sign the user out, and so "Ghi nhớ đăng nhập" actually
/// keeps the session across browser restarts. AuthState itself stays purely in-memory
/// (as before) - this service is only responsible for saving/restoring it.
///
/// - "Ghi nhớ đăng nhập" checked  -> localStorage (survives closing the browser).
/// - Unchecked                    -> sessionStorage (survives an F5 reload, cleared when
///                                    the tab/browser is closed) - this alone already fixes
///                                    the "mỗi lần load lại trang bị tự động đăng xuất" bug,
///                                    even for someone who never checks "remember me".
/// </summary>
public class AuthStorageService
{
    private const string TokenKey = "bms_auth_token";
    private const string RoleKey = "bms_auth_role";
    private const string NameKey = "bms_auth_fullname";

    private readonly IJSRuntime _js;

    public AuthStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task SaveAsync(string token, string role, string fullName, bool remember)
    {
        // Always clear both first so switching remember-me on/off doesn't leave a stale
        // copy of the token behind in the other storage.
        await ClearAsync();

        var storage = remember ? "localStorage" : "sessionStorage";
        await _js.InvokeVoidAsync($"{storage}.setItem", TokenKey, token);
        await _js.InvokeVoidAsync($"{storage}.setItem", RoleKey, role);
        await _js.InvokeVoidAsync($"{storage}.setItem", NameKey, fullName);
    }

    // Keeps the persisted copy of the display name in sync after a profile edit, so a
    // restored session (after F5) shows the up-to-date name instead of the one captured
    // at login time. Updates whichever storage currently holds the token.
    public async Task UpdateStoredNameAsync(string fullName)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey)))
            {
                await _js.InvokeVoidAsync("localStorage.setItem", NameKey, fullName);
            }
            if (!string.IsNullOrWhiteSpace(await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey)))
            {
                await _js.InvokeVoidAsync("sessionStorage.setItem", NameKey, fullName);
            }
        }
        catch
        {
            // Best-effort only - AuthState in memory is already correct either way.
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", RoleKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", NameKey);
            await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
            await _js.InvokeVoidAsync("sessionStorage.removeItem", RoleKey);
            await _js.InvokeVoidAsync("sessionStorage.removeItem", NameKey);
        }
        catch
        {
            // JS interop can fail very early (e.g. prerendering) - never let sign-out crash.
        }
    }

    /// <summary>
    /// Tries localStorage first (remembered session), then sessionStorage (this-tab-only
    /// session). Returns null if nothing is stored, or if the stored token has expired.
    /// </summary>
    public async Task<(string Token, string Role, string FullName)?> TryRestoreAsync()
    {
        try
        {
            var fromLocal = await TryReadAsync("localStorage");
            if (fromLocal is not null) return fromLocal;

            var fromSession = await TryReadAsync("sessionStorage");
            if (fromSession is not null) return fromSession;
        }
        catch
        {
            // No JS interop available (e.g. very early prerendering) - just skip restore.
        }

        return null;
    }

    private async Task<(string Token, string Role, string FullName)?> TryReadAsync(string storage)
    {
        var token = await _js.InvokeAsync<string?>($"{storage}.getItem", TokenKey);
        if (string.IsNullOrWhiteSpace(token)) return null;

        if (IsExpired(token))
        {
            await _js.InvokeVoidAsync($"{storage}.removeItem", TokenKey);
            await _js.InvokeVoidAsync($"{storage}.removeItem", RoleKey);
            await _js.InvokeVoidAsync($"{storage}.removeItem", NameKey);
            return null;
        }

        var role = await _js.InvokeAsync<string?>($"{storage}.getItem", RoleKey);
        var name = await _js.InvokeAsync<string?>($"{storage}.getItem", NameKey);
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(name)) return null;

        return (token, role, name);
    }

    // Decodes the JWT payload (no signature check needed here - the API still validates
    // the signature on every call) just to read "exp" and avoid restoring a dead session.
    private static bool IsExpired(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var expUnix))
            {
                var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                return expiresAtUtc <= DateTimeOffset.UtcNow;
            }
        }
        catch
        {
            // Malformed token - treat as expired/invalid rather than crash.
            return true;
        }

        return false;
    }
}
