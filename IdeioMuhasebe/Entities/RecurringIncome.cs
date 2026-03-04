using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdeioMuhasebe.Entities
{
    public class RecurringIncome
    {
        public int Id { get; set; }

        public int IncomeTypeId { get; set; }
        public IncomeType IncomeType { get; set; }

        [Required]
        public string Name { get; set; }

        // Şablon tutarları (Income oluşturulurken kopyalanır)
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Net + Tax

        public string? Payer { get; set; }

        public int DayOfMonth { get; set; }
        public DateTime StartDate { get; set; }

        // ✅ YENİ: Kaç dönem yenilenecek? (null/0 => sınırsız)
        public int? PeriodCount { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? UpdatedDate { get; set; }
    }
}