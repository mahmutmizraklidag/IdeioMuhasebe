using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Models
{
    public class DebtTypeUpsertVm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; } = null!;
    }
}