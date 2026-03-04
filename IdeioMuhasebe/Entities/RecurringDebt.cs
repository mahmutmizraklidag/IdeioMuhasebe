using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdeioMuhasebe.Entities
{
    public class RecurringDebt
    {
        public int Id { get; set; }

        public int DebtTypeId { get; set; }
        public DebtType DebtType { get; set; }

        [Required]
        public string Name { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string? Payee { get; set; }

        public int DayOfMonth { get; set; }
        public DateTime StartDate { get; set; }

        // ✅ YENİ: Kaç dönem yenilenecek? (null/0 => sınırsız)
        public int? PeriodCount { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? UpdatedDate { get; set; }
    }
}