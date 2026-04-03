using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdeioMuhasebe.Entities
{
    public class Income
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Gelir adı zorunludur.")]
        [Display(Name = "Gelir Adı")]
        public string Name { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = " Toplam Miktar (₺)")]
        public decimal Amount { get; set; }

        // ✅ YENİ: Net
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Miktar (₺)")]
        public decimal NetAmount { get; set; }

        // ✅ YENİ: Vergi
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Vergi Miktarı (₺)")]
        public decimal TaxAmount { get; set; }
        // ✅ YENİ: Vergi
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReceivedAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal CarryForwardBalance { get; set; } = 0m;
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Tahsil Tarihi")]
        public DateTime DueDate { get; set; } // Gider ile aynı mantık (JS kolaylığı)

        [Display(Name = "Kimden / Kaynak")]
        public string? Payer { get; set; }

        [Display(Name = "Tahsil Edildi mi?")]
        public bool IsReceived { get; set; }

        [Display(Name = "Son Güncelleme")]
        public DateTime? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; } = false;
        public int IncomeTypeId { get; set; }
        public IncomeType IncomeType { get; set; }

        // ✅ YENİ: Yinelenen gelir şablonu bağlantısı
        public int? RecurringIncomeId { get; set; }
        public RecurringIncome? RecurringIncome { get; set; }
      
    }
}