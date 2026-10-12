namespace BMS.API.Models;

public class CueRentalEntity
{
    public int Id { get; set; }
    public int CueId { get; set; }
    public CueEntity? Cue { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime RentedAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public string? Note { get; set; }

    // "Active" | "Returned" | "Damaged" | "Lost"
    public string Status { get; set; } = "Active";
}
