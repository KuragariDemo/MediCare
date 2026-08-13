using System;
using System.ComponentModel.DataAnnotations;

namespace MediCare.App.ViewModels.Patient
{
    public class PatientProfileVM
    {
        // Hidden / internal
        public int PatientId { get; set; }
        public string UserId { get; set; } = default!;

        // Display + edit
        [Required, StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = default!;

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = default!;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [StringLength(200)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        [Display(Name = "Gender")]
        public string? Gender { get; set; }

        public string DisplayName { get; set; } = "Patient";
        public string Initials { get; set; } = "PT";
        public string? AvatarUrl { get; set; }

        public string? StatusMessage { get; set; }
    }
}
