using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.App.Models.ViewModels
{
    public class AppointmentBookVm
    {
        // Doctor
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";

        // Slot
        [Required] public DateTime DutyDate { get; set; }
        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }
        [Required, MaxLength(128)] public string Branch { get; set; } = "";

        // Patient info
        [Required, MaxLength(128)] public string PatientName { get; set; } = "";
        [Required, EmailAddress, MaxLength(128)] public string PatientEmail { get; set; } = "";
        [Required, MaxLength(32)] public string PatientPhone { get; set; } = "";
        [MaxLength(500)] public string? ReasonNote { get; set; }

        // Money (snapshotted at booking)
        public decimal ConsultationFee { get; set; }
        public decimal TotalAmount { get; set; }

        // Payment choice
        // "cash" | "card"
        public string PaymentChoice { get; set; } = "cash";

        // Demo card fields (for card flow)
        public string? CardNumber { get; set; }
        public string? CardName { get; set; }
        public string? CardExp { get; set; }
        public string? CardCvc { get; set; }
    }
}
