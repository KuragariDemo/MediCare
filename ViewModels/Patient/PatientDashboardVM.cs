using System;
using System.Collections.Generic;

namespace MediCare.App.ViewModels.Patient
{
    public class PatientDashboardVM
    {
        public string DisplayName { get; set; } = "Patient";
        public string Initials { get; set; } = "PT";
        public int PatientId { get; set; }

        public int UpcomingAppointmentsCount { get; set; }
        public int UnpaidBillsCount { get; set; }
        public decimal UnpaidBillsTotal { get; set; }
        public int PrescriptionsCount { get; set; }

        public UpcomingAppointmentVM? NextAppointment { get; set; }
        public List<UpcomingAppointmentVM> UpcomingAppointments { get; set; } = new();
    }

    public class UpcomingAppointmentVM
    {
        public int AppointmentId { get; set; }
        public string DoctorName { get; set; } = "";
        public string DoctorSpecialty { get; set; } = "";
        public string Branch { get; set; } = "";
        public DateTime DutyDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
