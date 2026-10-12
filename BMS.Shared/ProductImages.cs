namespace BMS.Shared;

/// <summary>
/// Ảnh minh họa có sẵn trong wwwroot/images của từng web (bộ ảnh thiết kế D:\bms-anh) cho
/// cơ, phụ kiện và món ăn/đồ uống CHƯA có ảnh tải lên (ImageUrl rỗng). Chỉ chọn ảnh theo
/// tên/hãng/danh mục để hiển thị - không ghi gì vào dữ liệu. Ảnh do Admin tải lên luôn được
/// ưu tiên hơn ảnh ở đây.
/// Giá trị trả về là "đường dẫn gốc" không kèm kích thước, ví dụ "images/products/cue-fury";
/// mỗi ảnh có 2 bản WebP: "-320.webp" (320x240) và "-640.webp" (640x480) - riêng ảnh món
/// ăn/đồ uống (chỉ có ở cổng nhân viên) là "-160.webp" và "-320.webp".
/// </summary>
public static class ProductImages
{
    public const string ProductFolder = "images/products/";
    public const string FnbFolder = "images/fnb/";

    private static readonly (string Keyword, string File)[] CueRules =
    {
        ("predator", "cue-predator"),
        ("cuetec", "cue-cuetec-cynergy"),
        ("mezz", "cue-mezz-exceed"),
        ("longoni", "cue-mezz-exceed"),
        ("musashi", "cue-musashi"),
        ("rhino", "cue-rhino"),
        ("fury", "cue-fury"),
        ("aramith", "pool-balls"),
        ("bộ bi", "pool-balls"),
        ("tam giác", "pool-balls"),
        ("cao cấp", "cue-musashi"),
        ("thi đấu", "cue-rhino"),
        ("tiêu chuẩn", "cue-fury"),
    };

    private static readonly (string Keyword, string File)[] FnbRules =
    {
        ("red bull", "red-bull"),
        ("bò húc", "red-bull"),
        ("sting", "sting-dau"),
        ("nước suối", "aquafina"),
        ("aquafina", "aquafina"),
        ("nước ngọt", "coca-cola"),
        ("coca", "coca-cola"),
        ("pepsi", "coca-cola"),
        ("khoai tây", "khoai-tay-chien"),
        ("cánh gà", "canh-ga"),
        ("khô gà", "canh-ga"),
        ("xúc xích", "xuc-xich"),
        ("nướng", "xuc-xich"),
        ("cơm chiên", "com-chien"),
        ("mì", "mi-goi"),
        ("cà phê", "ca-phe-sua-da"),
        ("bạc xỉu", "ca-phe-sua-da"),
        ("matcha", "matcha-latte"),
        ("trà sữa", "matcha-latte"),
        ("trà", "tra-da"),
    };

    /// <summary>Ảnh cho cơ / phụ kiện (dùng ở cả PlayerWeb và cổng nhân viên).</summary>
    public static string ForCue(string? name, string? brand, string? category)
    {
        var text = $"{brand} {name}".ToLowerInvariant();
        foreach (var (keyword, file) in CueRules)
        {
            if (text.Contains(keyword))
            {
                return ProductFolder + file;
            }
        }

        // Phụ kiện khác (găng tay, giá đỡ...) dùng ảnh bộ sưu tập cơ; cơ chưa nhận diện được
        // dùng ảnh cơ Fury.
        var isAccessory = category is not null && category.Contains("phụ kiện", StringComparison.OrdinalIgnoreCase);
        return ProductFolder + (isAccessory ? "cues-collection" : "cue-fury");
    }

    /// <summary>Ảnh cho món ăn / đồ uống trong kho (chỉ có ở cổng nhân viên). Trả về null
    /// khi không có ảnh phù hợp (ví dụ thuốc lá) - giao diện hiện icon thay thế.</summary>
    public static string? ForFnb(string? name, string? category)
    {
        var text = (name ?? string.Empty).ToLowerInvariant();
        foreach (var (keyword, file) in FnbRules)
        {
            if (text.Contains(keyword))
            {
                return FnbFolder + file;
            }
        }

        var cat = (category ?? string.Empty).ToLowerInvariant();
        if (cat.Contains("đồ ăn")) return FnbFolder + "snack-menu";
        if (cat.Contains("uống") || cat.Contains("bia") || cat.Contains("trà") || cat.Contains("cà phê")) return FnbFolder + "drinks-menu";
        return null;
    }
}
