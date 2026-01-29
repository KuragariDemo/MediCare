using System;
using System.Collections.Generic;

namespace MediCare.App.ViewModels.Doctor
{
    // One row in the table
    public class PatientRecordRowVM
    {
        public int AppointmentId { get; set; }

        // Patient snapshot (from Appointment)
        public string PatientName { get; set; } = "";
        public string PatientEmail { get; set; } = "";
        public string? ReasonNote { get; set; }

        // When the appointment happened/is scheduled
        public DateTime DutyDate { get; set; }
        public TimeSpan StartTime { get; set; }

        // Feedback (patient side)
        public bool HasFeedback { get; set; }
        public string? FeedbackText { get; set; }

        // Prescription (doctor side)
        public bool HasPrescription { get; set; }
        public string? PrescriptionText { get; set; }
    }

    // Page VM given to the view
    public class PatientRecordPageVM
    {
        public List<PatientRecordRowVM> Records { get; set; } = new();
        public string DoctorDisplayName { get; set; } = "";
    }
}
