// ViewModels/DoctorCreateVm.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MediCare.App.ViewModels
{
    public class DoctorCreateVm
    {
        [Required, StringLength(120)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Please select a specialty.")]
        public int? SpecialtyId { get; set; }

        public string? Bio { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; } = "";

        [Required, Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = "";

        // For the dropdown
        public IEnumerable<SelectListItem> Specialties { get; set; } = new List<SelectListItem>();
    }
}
