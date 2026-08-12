using System.ComponentModel.DataAnnotations;

namespace MediCare.App.ViewModels
{
    public class ContactMessageVm
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = "";

        [Required, EmailAddress, MaxLength(120)]
        public string Email { get; set; } = "";

        [MaxLength(30)]
        public string? Phone { get; set; }

        [Required, MaxLength(200)]
        public string Subject { get; set; } = "";

        [Required, MaxLength(2000)]
        public string Message { get; set; } = "";
    }
}
