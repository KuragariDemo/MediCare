namespace MediCare.App.Models
{
    public enum AdminLevel { AdminPlus = 1, Admin = 2 }

    public class AdminProfile
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        public AdminLevel Level { get; set; } = AdminLevel.Admin;
        public bool IsActive { get; set; } = true;

        public string? CreatedByUserId { get; set; } // who created this admin
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
