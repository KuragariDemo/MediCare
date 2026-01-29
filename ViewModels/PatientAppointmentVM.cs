// ViewModels/PatientAppointmentVM.cs
using System;

namespace MediCare.App.ViewModels.Patient
{
    public class PatientAppointmentRowVM
    {
        public int AppointmentId { get; set; }

        public string DoctorName { get; set; } = default!;
        public string DoctorSpecialty { get; set; } = default!;

        public string BranchName { get; set; } = default!;
        public string? BranchAddress { get; set; }

        // current booking slot shown in the table
        public DateTime LocalDateTimeStart { get; set; } // e.g. 2026-02-10 10:00 in clinic TZ
        public DateTime LocalDateTimeEnd { get; set; }   // optional to show later

        // for data-* attributes in <tr>
        public int DoctorId { get; set; }
    }

    public class PatientMyAppointmentsPageVM
    {
        public List<PatientAppointmentRowVM> Items { get; set; } = new();
    }
}
