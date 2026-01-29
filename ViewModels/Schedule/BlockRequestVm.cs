// ViewModels/Schedule/BlockRequestVm.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.ViewModels.Schedule
{
    public class BlockRequestVm
    {
        [Required] public string DoctorId { get; set; } = default!; // ← string (UserId)

        [Required, DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }

        // No reason field since you said don't add it
    }
}
