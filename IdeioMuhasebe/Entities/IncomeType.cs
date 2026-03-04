using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Entities
{
    public class IncomeType
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Gelir türü adı zorunludur.")]
        [Display(Name = "Gelir Türü (Örn: Maaş, Kira, Tahsilatlar)")]
        public string Name { get; set; }

        public ICollection<Income> Incomes { get; set; }
    }
}