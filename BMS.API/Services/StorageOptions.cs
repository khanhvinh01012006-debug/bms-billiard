namespace BMS.API.Services;

/// <summary>
/// Vị trí lưu ảnh upload. UploadsDirectory = null nghĩa là giữ cách cũ (wwwroot/uploads),
/// khác null khi cấu hình Storage:DataDirectory (ví dụ volume Docker /data).
/// </summary>
public record StorageOptions(string? UploadsDirectory);
