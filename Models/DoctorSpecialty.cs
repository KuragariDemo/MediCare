using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DoctorSpecialty
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = default!;

    [StringLength(120)]
    public string? Slug { get; set; }

    // NEW 👇
    [Range(0, 999999)]
    [Column(TypeName = "decimal(10,2)")] // SQL Server money-safe precision
    public decimal Fee { get; set; } = 0m;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
