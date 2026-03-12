using System;

namespace IdeioMuhasebe.Models
{
    public class IncomeUpsertVm
    {
        public int Id { get; set; }
        public int IncomeTypeId { get; set; }
        public string Name { get; set; }

        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string? Payer { get; set; }
        public bool IsReceived { get; set; }

        public bool IsRecurring { get; set; }

        // ✅ YENİ: Kaç dönem (null/0 => sınırsız)
        public int? PeriodCount { get; set; }
    }
}