using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Entities
{
    public class DebtType
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Borç türü adı zorunludur.")]
        [Display(Name = "Borç Türü (Örn: Faturalar, Vergiler)")]
        public string Name { get; set; }

        // Bire-Çok İlişki: Bir türün birden fazla borcu olabilir
        public ICollection<Debt> Debts { get; set; }
    }
}
