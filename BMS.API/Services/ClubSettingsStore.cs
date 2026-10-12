using System.Text.Json;
using BMS.Shared.Dtos;

namespace BMS.API.Services;

/// <summary>
/// Đọc/ghi cấu hình chung (settings.json). Có Storage:DataDirectory thì file nằm trong thư mục
/// dữ liệu (volume Docker, không mất khi build lại), không thì nằm cạnh ứng dụng (App_Data).
/// </summary>
public class ClubSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ClubSettingsDto? _cache;

    public ClubSettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public async Task<ClubSettingsDto> GetAsync()
    {
        if (_cache is not null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache is null && File.Exists(_path))
            {
                await using var stream = File.OpenRead(_path);
                _cache = await JsonSerializer.DeserializeAsync<ClubSettingsDto>(stream, JsonOptions);
            }
            return _cache ??= new ClubSettingsDto();
        }
        catch (JsonException)
        {
            // File hỏng: dùng mặc định, lần lưu sau sẽ ghi đè.
            return _cache = new ClubSettingsDto();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ClubSettingsDto settings)
    {
        await _lock.WaitAsync();
        try
        {
            var temp = _path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, _path, overwrite: true);
            _cache = settings;
        }
        finally
        {
            _lock.Release();
        }
    }
}
