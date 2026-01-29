// ViewModels/Schedule/CreateAssignmentVm.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.ViewModels.Schedule
{
    public class CreateAssignmentVm
    {
        [Required] public int DoctorId { get; set; }
        [Required] public int BranchId { get; set; }

        // Date and times come from simple inputs; we’ll merge them.
        [Required, DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required, DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required, DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }

        // For display
        public string? Error { get; set; }
        public double RemainingHoursThisWeek { get; set; }
    }
}
