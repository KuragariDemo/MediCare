using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.ViewModels.Schedule
{
    public class LeaveRequestCreateVm
    {
        [Required] public string DoctorId { get; set; } = default!;
        [Required, DataType(DataType.Date)] public DateTime StartDate { get; set; }
        [Required, DataType(DataType.Date)] public DateTime EndDate { get; set; }
        [StringLength(512)] public string? Reason { get; set; }
    }
}
