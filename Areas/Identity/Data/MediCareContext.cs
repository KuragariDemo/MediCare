using MediCare.App.Models;
using MediCare.App.Models.Scheduling;
using MediCare.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediCare.App.Data
{
    public class MediCareContext : IdentityDbContext<ApplicationUser>
    {
        public MediCareContext(DbContextOptions<MediCareContext> options) : base(options) { }

        public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<ClinicBranch> ClinicBranches => Set<ClinicBranch>();
        public DbSet<DutyAssignment> DutyAssignments => Set<DutyAssignment>();
        public DbSet<DoctorBlockRequest> DoctorBlockRequests => Set<DoctorBlockRequest>();
        public DbSet<DoctorSpecialty> DoctorSpecialties => Set<DoctorSpecialty>();      
        public DbSet<DoctorDuty> DoctorDuties { get; set; } = default!;
        public DbSet<Appointment> Appointments { get; set; } = default!;
        public DbSet<AppointmentPrescription> AppointmentPrescriptions { get; set; } = default!;
        public DbSet<AppointmentFeedback> AppointmentFeedbacks { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<AdminProfile>().HasIndex(x => x.UserId).IsUnique();
            b.Entity<Doctor>().HasIndex(x => x.UserId).IsUnique();
            b.Entity<Patient>().HasIndex(x => x.UserId).IsUnique();

            b.Entity<AdminProfile>()
                .HasOne(a => a.User).WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<Doctor>()
                .ToTable("Doctors")
                .HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<Doctor>()
                .HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Patient>()
                .HasOne(p => p.User).WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<Doctor>()
                .Property(d => d.WeeklyHourLimit)
                .HasDefaultValue(48);

            b.Entity<DutyAssignment>(e =>
            {
                e.HasIndex(x => new { x.DoctorId, x.StartUtc });
                e.HasIndex(x => x.BranchId);
                e.HasCheckConstraint("CK_DutyAssignment_Time", "[EndUtc] > [StartUtc]");
            });

            b.Entity<DoctorBlockRequest>(e =>
            {
                e.HasIndex(x => new { x.DoctorId, x.Status });
                e.HasCheckConstraint("CK_BlockRequest_Dates", "[EndDateUtc] >= [StartDateUtc]");
            });

            b.Entity<DoctorSpecialty>(e =>
            {
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.Name).HasMaxLength(80).IsRequired();
                e.Property(x => x.Slug).HasMaxLength(120);
            });

            b.Entity<Doctor>()
             .HasOne(d => d.Specialty)
             .WithMany()
             .HasForeignKey(d => d.SpecialtyId)
             .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
