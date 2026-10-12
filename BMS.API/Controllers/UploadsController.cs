using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BMS.API.Services;
using BMS.Shared.Dtos;

namespace BMS.API.Controllers;

[ApiController]
[Route("api/uploads")]
// Nới quyền: bất kỳ ai đã đăng nhập (Player/Staff/Admin) đều upload được ảnh, cần cho
// ảnh chợ trao đổi cơ (Trade) và ảnh gửi trong khung chat. Vẫn giữ giới hạn loại
// file/kích thước như cũ.
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly StorageOptions _storage;

    public UploadsController(IWebHostEnvironment env, StorageOptions storage)
    {
        _env = env;
        _storage = storage;
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"
    };

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<UploadImageResultDto>> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Chưa chọn ảnh.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest("Định dạng ảnh không được hỗ trợ.");
        }

        string webRootPath = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRootPath))
        {
            webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        // Có Storage:DataDirectory thì lưu vào thư mục dữ liệu (volume Docker), không thì wwwroot/uploads.
        var uploadsFolder = _storage.UploadsDirectory ?? Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // IMPORTANT: must be an ABSOLUTE URL. Admin/Player Web and the MAUI app run on
        // different origins/ports than the API, so a relative "/uploads/xxx" URL would
        // resolve against the CLIENT's own origin and fail to load the image.
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

        return new UploadImageResultDto(absoluteUrl);
    }
}
