using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.App.Models.Scheduling
{
    public enum BlockRequestStatus { Pending = 0, Approved = 1, Rejected = 2, Cancelled = 3 }

    public class DoctorBlockRequest
    {
        public int Id { get; set; }

        [Required] public string DoctorId { get; set; } = default!; // ApplicationUser.Id

        [Required] public DateTime StartDateUtc { get; set; }
        [Required] public DateTime EndDateUtc { get; set; }

        [StringLength(512)] public string? Reason { get; set; }

        [Required] public BlockRequestStatus Status { get; set; } = BlockRequestStatus.Pending;

        // audit
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
        public string? DecisionByUserId { get; set; }
        public DateTime? DecisionAtUtc { get; set; }
    }
}
