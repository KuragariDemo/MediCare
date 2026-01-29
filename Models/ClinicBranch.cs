// Models/ClinicBranch.cs
using System.ComponentModel.DataAnnotations;

namespace MediCare.Models
{
    public class ClinicBranch
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = default!;

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [EmailAddress, StringLength(120)]
        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
