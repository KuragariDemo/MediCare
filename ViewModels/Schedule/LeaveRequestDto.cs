using System;

namespace MediCare.ViewModels.Schedule
{
    public class LeaveRequestDto
    {
        public int Id { get; set; }
        public string DoctorId { get; set; } = default!;
        public string Status { get; set; } = "pending";
        public string Start { get; set; } = default!;  // ISO yyyy-MM-dd (local)
        public string End { get; set; } = default!;
        public string Submitted { get; set; } = default!;
        public string? Reason { get; set; }
    }
}
