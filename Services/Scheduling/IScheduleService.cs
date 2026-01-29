// Services/Scheduling/IScheduleService.cs
using MediCare.App.Models.Scheduling;

namespace MediCare.App.Services.Scheduling
{
    // Use this record in both interface & service
    public record WeeklyHoursResult(double TotalHours, double HoursLeft, List<DutyAssignment> Assignments);

    public interface IScheduleService
    {
        Task<WeeklyHoursResult> GetWeeklyHoursAsync(
            string doctorId,
            DateTime weekAnyLocalDate,
            TimeZoneInfo tz,
            CancellationToken ct);

        Task<(bool ok, string? error)> AssignDutyAsync(
            string doctorId,
            int branchId,
            DateTime startLocal,
            DateTime endLocal,
            string createdByUserId,
            TimeZoneInfo tz,
            CancellationToken ct);

        Task<bool> ApproveBlockAsync(
            int requestId,
            string adminUserId,
            CancellationToken ct);

        Task<bool> DeclineBlockAsync(
            int requestId,
            string adminUserId,
            CancellationToken ct);

        // NEW: no reason, optional ct (default)
        Task<(bool ok, string? error, int requestId)> RequestBlockAsync(
            string doctorId,
            DateTime startLocal,
            DateTime endLocal,
            TimeZoneInfo tz,
            CancellationToken ct = default);

        Task<(bool ok, string? error)> UpdateAssignmentAsync(
        int assignmentId,
        string newDoctorId,
        int newBranchId,
        DateTime newStartLocal,
        DateTime newEndLocal,
        TimeZoneInfo tz,
        CancellationToken ct);

        Task<(bool ok, string? error)> DeleteAssignmentAsync(
            int assignmentId,
            CancellationToken ct);
    }


}
