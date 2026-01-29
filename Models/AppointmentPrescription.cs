using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediCare.App.Models
{
    public class AppointmentPrescription
    {
        public int Id { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment Appointment { get; set; } = default!;

        // big text, doctor's notes / medicine list
        [Required]
        [MaxLength(2000)]
        public string PrescriptionText { get; set; } = default!;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
