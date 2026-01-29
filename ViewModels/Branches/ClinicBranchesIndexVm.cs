using MediCare.Models;
using System.ComponentModel.DataAnnotations;

namespace MediCare.ViewModels.Branches
{
    public class ClinicBranchesIndexVm
    {
        public IReadOnlyList<ClinicBranch> Items { get; set; } = Array.Empty<ClinicBranch>();
        public string? Query { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;

        public class BranchForm
        {
            public int? Id { get; set; }
            [Required, StringLength(200)]
            public string Name { get; set; } = default!;
            public string? Address { get; set; }
            [Phone] public string? Phone { get; set; }
            [EmailAddress] public string? Email { get; set; }
        }
    }
}
