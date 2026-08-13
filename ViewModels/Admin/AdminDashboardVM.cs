using System;
using System.Collections.Generic;

namespace MediCare.App.ViewModels.Admin
{
    public class AdminDashboardVM
    {
        public int TotalDoctors { get; set; }
        public int TotalPatients { get; set; }
        public int TodayAppointments { get; set; }
        public int TodayPendingPayments { get; set; }
        public decimal RevenueThisMonth { get; set; }

        public List<DayCount> Last7Days { get; set; } = new();
        public List<DeptShare> DepartmentShares { get; set; } = new();
        public List<RecentBooking> RecentBookings { get; set; } = new();
    }

    public class DayCount
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class DeptShare
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public int Percent { get; set; }
    }

    public class RecentBooking
    {
        public string PatientName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public decimal Amount { get; set; }
        public bool Paid { get; set; }
    }
}
