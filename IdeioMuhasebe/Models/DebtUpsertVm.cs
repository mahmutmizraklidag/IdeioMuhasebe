using System;

namespace IdeioMuhasebe.Models
{
    public class DebtUpsertVm
    {
        public int Id { get; set; }
        public int DebtTypeId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string? Payee { get; set; }
        public bool IsPaid { get; set; }

        public bool IsRecurring { get; set; }

        // ✅ YENİ: Kaç dönem (null/0 => sınırsız)
        public int? PeriodCount { get; set; }
    }
}