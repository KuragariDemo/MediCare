// Models/Scheduling/DutyAssignment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediCare.App.Models.Scheduling
{
    public class DutyAssignment
    {
        public int Id { get; set; }
        [Required] public string DoctorId { get; set; } = default!;
        [Required] public int BranchId { get; set; }
        [Required] public DateTime StartUtc { get; set; }
        [Required] public DateTime EndUtc { get; set; }
        [StringLength(64)] public string? CreatedByUserId { get; set; }
        [NotMapped] public double Hours => (EndUtc - StartUtc).TotalHours;
    }
}
