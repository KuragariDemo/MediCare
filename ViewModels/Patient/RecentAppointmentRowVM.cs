using System;
using System.Collections.Generic;

namespace MediCare.App.ViewModels.Patient
{
    public class RecentAppointmentRowVM
    {
        public int AppointmentId { get; set; }

        public DateTime DutyDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Doctor info
        public string DoctorName { get; set; } = "";
        public string DoctorEmail { get; set; } = "";
        public string DoctorSpecialty { get; set; } = "";

        // Prescription
        public bool HasPrescription { get; set; }          // true = Submitted pill
        public string? PrescriptionText { get; set; }      // show in modal

        // Feedback
        public string? ExistingFeedbackText { get; set; }  // we can preload if needed
    }
}

namespace MediCare.App.ViewModels.Patient
{
    public class RecentAppointmentsVM
    {
        public List<RecentAppointmentRowVM> Appointments { get; set; } = new();
    }
}
