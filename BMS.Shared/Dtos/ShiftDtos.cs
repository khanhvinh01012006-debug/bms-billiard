namespace BMS.Shared.Dtos;

// Đợt 4: Bàn giao ca làm việc.

public record ShiftDto(
    int Id,
    int StaffAccountId,
    string StaffName,
    DateTime StartedAtUtc,
    decimal OpeningCashAmount,
    DateTime? EndedAtUtc,
    decimal? ClosingCashAmount,
    string? HandoverNote,
    string Status);

public record StartShiftRequestDto(decimal OpeningCashAmount);

public record EndShiftRequestDto(decimal ClosingCashAmount, string? HandoverNote);

// Đợt 9: Dashboard - phát hiện lệch quỹ tiền mặt cuối ca so với dự kiến.
public record ShiftCashDiscrepancyDto(
    int ShiftId, string StaffName, DateTime StartedAtUtc, DateTime? EndedAtUtc,
    decimal OpeningCashAmount, decimal? ClosingCashAmount, decimal ExpectedClosingCashAmount, decimal Discrepancy);
