using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdeioMuhasebe.Entities
{
    public class Debt
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Borç adı zorunludur.")]
        [Display(Name = "Borç Adı")]
        public string Name { get; set; }

        // ✅ TOPLAM (Net + Vergi)
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Toplam (₺)")]
        public decimal Amount { get; set; }

        // ✅ YENİ: Net
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        // ✅ YENİ: Vergi
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PaidAmount { get; set; }
        // + ise devreden eksik ödeme
        // - ise devreden fazla ödeme kredisi
        [Column(TypeName = "decimal(18,2)")]
        public decimal CarryForwardBalance { get; set; } = 0m;
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Son Ödeme Tarihi")]
        public DateTime DueDate { get; set; }

        [Display(Name = "Ödenecek Yer / Kurum")]
        public string? Payee { get; set; }

        [Display(Name = "Ödendi mi?")]
        public bool IsPaid { get; set; }

        // YENİ EKLENEN ALAN: Güncelleme Tarihi
        [Display(Name = "Son Güncelleme")]
        public DateTime? UpdatedDate { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Yabancı Anahtar (Foreign Key)
        public int DebtTypeId { get; set; }

        // Navigasyon Property
        [ForeignKey("DebtTypeId")]
        public DebtType DebtType { get; set; }

        public int? RecurringDebtId { get; set; }
        public RecurringDebt? RecurringDebt { get; set; }
    }
}
