using System;
using System.Collections.Generic;

namespace MediCare.App.ViewModels.Doctor
{
    public class DoctorDashboardVM
    {
        public int TodayCount { get; set; }
        public int TodayCompletedCount { get; set; }
        public int TodayUpcomingCount { get; set; }
        public int TotalPatientsCount { get; set; }

        public List<TodayAppointmentVM> TodaySchedule { get; set; } = new();
        public List<RecentPatientVM> RecentPatients { get; set; } = new();
    }

    public class TodayAppointmentVM
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = "";
        public string? ReasonNote { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsInProgress { get; set; }
    }

    public class RecentPatientVM
    {
        public string PatientName { get; set; } = "";
        public DateTime LastVisit { get; set; }
        public string? ReasonNote { get; set; }
        public bool Paid { get; set; }
    }
}
