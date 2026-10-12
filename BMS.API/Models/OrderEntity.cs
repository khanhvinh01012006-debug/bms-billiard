namespace BMS.API.Models;

// Đợt 4: Gọi món & dịch vụ - một dòng order (đồ uống/snack/dịch vụ) gắn với một
// PlayingSession cụ thể (không gắn trực tiếp với bàn, để lịch sử vẫn đúng khi bàn được
// tái sử dụng cho khách khác). ItemName/UnitPrice được snapshot lúc thêm để không bị ảnh
// hưởng nếu giá vật tư trong Kho thay đổi sau đó.
public class OrderEntity
{
    public int Id { get; set; }
    public int PlayingSessionId { get; set; }
    public PlayingSessionEntity? Session { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    // Optional link back to the Inventory item this order was drawn from - null when
    // staff added a free-text service line that isn't tracked in Inventory.
    public int? InventoryItemId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public int CreatedByAccountId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}
