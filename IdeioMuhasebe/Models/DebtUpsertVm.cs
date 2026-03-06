using System;

namespace IdeioMuhasebe.Models
{
    public class DebtUpsertVm
    {
        public int Id { get; set; }
        public int DebtTypeId { get; set; }
        public string Name { get; set; }
        // ✅ yeni alanlar
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }

        // ✅ geriye dönük uyum (eski UI)
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string? Payee { get; set; }
        public bool IsPaid { get; set; }

        public bool IsRecurring { get; set; }

        // ✅ YENİ: Kaç dönem (null/0 => sınırsız)
        public int? PeriodCount { get; set; }
    }
}