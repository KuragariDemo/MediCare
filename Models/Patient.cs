using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediCare.App.Models
{
    public class Patient
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        // --- Profile Info ---
        [Required, StringLength(100)]
        public string FullName { get; set; } = default!;

        [Phone]
        public string? PhoneNumber { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }
    }
}
