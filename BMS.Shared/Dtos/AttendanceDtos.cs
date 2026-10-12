namespace BMS.Shared.Dtos;

// Đợt 4: Chấm công (đơn giản hơn Ca làm việc - chỉ phục vụ payroll/HR).

public record AttendanceDto(
    int Id,
    int StaffAccountId,
    string StaffName,
    DateTime ClockInAtUtc,
    DateTime? ClockOutAtUtc,
    string? Note,
    double? HoursWorked);

public record ClockInRequestDto(string? Note);

public record ClockOutRequestDto(string? Note);
