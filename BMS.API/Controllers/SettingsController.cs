using System.Text.RegularExpressions;
using BMS.API.Services;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

// Cấu hình chung của quán: thông tin liên hệ (footer trang người chơi) và tài khoản nhận
// chuyển khoản VietQR (màn thanh toán của nhân viên). Ai cũng đọc được, chỉ Admin được sửa.
[ApiController]
[Route("api/settings")]
public partial class SettingsController : ControllerBase
{
    private readonly ClubSettingsStore _store;

    public SettingsController(ClubSettingsStore store) => _store = store;

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<ClubSettingsDto>> Get() => await _store.GetAsync();

    [Authorize(Roles = "Admin")]
    [HttpPut]
    public async Task<ActionResult<ClubSettingsDto>> Update(ClubSettingsDto request)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        var settings = new ClubSettingsDto(
            ClubName: Clean(request.ClubName) ?? "",
            Hotline: Clean(request.Hotline),
            Email: Clean(request.Email),
            FacebookUrl: Clean(request.FacebookUrl),
            Address: Clean(request.Address),
            Website: Clean(request.Website),
            BankBin: Clean(request.BankBin),
            BankAccountNumber: Clean(request.BankAccountNumber),
            BankAccountName: Clean(request.BankAccountName)?.ToUpperInvariant());

        if (settings.ClubName.Length is 0 or > 100) return BadRequest("Tên quán không được để trống (tối đa 100 ký tự).");
        if (settings.Email is not null && !settings.Email.Contains('@')) return BadRequest("Email không hợp lệ.");
        foreach (var url in new[] { settings.FacebookUrl, settings.Website })
        {
            if (url is not null && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
            {
                return BadRequest("Đường dẫn Facebook/Website phải bắt đầu bằng http:// hoặc https://.");
            }
        }
        if (settings.BankBin is not null && !BinRegex().IsMatch(settings.BankBin)) return BadRequest("Mã BIN ngân hàng gồm 6 chữ số.");
        if (settings.BankAccountNumber is not null && !AccountRegex().IsMatch(settings.BankAccountNumber))
        {
            return BadRequest("Số tài khoản chỉ gồm chữ số (6-19 số).");
        }
        if ((settings.BankBin is null) != (settings.BankAccountNumber is null))
        {
            return BadRequest("Cần nhập đủ cả mã BIN và số tài khoản ngân hàng (hoặc bỏ trống cả hai).");
        }

        await _store.SaveAsync(settings);
        return settings;
    }

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex BinRegex();

    [GeneratedRegex(@"^\d{6,19}$")]
    private static partial Regex AccountRegex();
}
