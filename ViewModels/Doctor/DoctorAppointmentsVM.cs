using System;
using System.Collections.Generic;

namespace MediCare.App.ViewModels.Doctor
{
    public class DoctorAppointmentsVM
    {
        public string SearchTerm { get; set; } = "";
        public DateOnly TodayDate { get; set; }

        public List<AppointmentRow> AllAppointments { get; set; } = new();
        public List<AppointmentRow> TodayAppointments { get; set; } = new();

        public class AppointmentRow
        {
            public string PatientName { get; set; } = "";
            public string PatientEmail { get; set; } = "";
            public string PatientPhone { get; set; } = "";

            public string Reason { get; set; } = "";
            public string Gender { get; set; } = ""; // we will pull this from Patient table

            public DateTime StartDateTimeLocal { get; set; } // DutyDate + StartTime
            public string BranchName { get; set; } = "";
        }
    }
}
