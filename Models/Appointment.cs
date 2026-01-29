using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.App.Models
{
    public enum PaymentMethod { CashAtClinic = 0, Card = 1 }
    public enum PaymentStatus { Pending = 0, Paid = 1, Failed = 2, Refunded = 3 }

    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public string PatientId { get; set; } = default!; // FK -> user

        [Required]
        public int DoctorId { get; set; } // FK -> Doctor.Id

        // 🟢 ADD THIS so .Include(a => a.Doctor) works
        public Doctor Doctor { get; set; } = default!;

        // Slot info
        [Required]
        public DateTime DutyDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required, MaxLength(128)]
        public string Branch { get; set; } = default!;

        // Patient snapshot (stored at booking time)
        [Required, MaxLength(128)]
        public string PatientName { get; set; } = default!;

        [Required, EmailAddress, MaxLength(128)]
        public string PatientEmail { get; set; } = default!;

        [Required, MaxLength(32)]
        public string PatientPhone { get; set; } = default!;

        [MaxLength(500)]
        public string? ReasonNote { get; set; }

        // Money snapshot
        [Range(0, 999999)]
        public decimal ConsultationFee { get; set; }

        [Range(0, 999999)]
        public decimal TotalAmount { get; set; } // <-- this is your final charge

        // Payment
        public PaymentMethod PaymentMethod { get; set; }
        public PaymentStatus PaymentStatus { get; set; }

        [MaxLength(64)]
        public string? PaymentRef { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public AppointmentPrescription? Prescription { get; set; }
        public AppointmentFeedback? Feedback { get; set; }

    }
}
