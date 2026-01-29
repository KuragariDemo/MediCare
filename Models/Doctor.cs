// Models/Doctor.cs
using MediCare.App.Models.Scheduling;
using MediCare.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediCare.App.Models
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = default!;  // FK -> AspNetUsers.Id

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string Speciality { get; set; } = default!; 

        public string? Bio { get; set; }

        // Audit trail: who created this doctor account
        public string? CreatedByUserId { get; set; }
        [ForeignKey(nameof(CreatedByUserId))]
        public ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public int? SpecialtyId { get; set; }
        public DoctorSpecialty? Specialty { get; set; }
        public int WeeklyHourLimit { get; set; } = 48; 
        public ICollection<DutyAssignment> Assignments { get; set; } = new List<DutyAssignment>();
        public ICollection<DoctorBlockRequest> BlockedTimes { get; set; } = new List<DoctorBlockRequest>();

        [NotMapped]
        public string DoctorCode => $"DR-{Id.ToString("D3")}";

    }
}
