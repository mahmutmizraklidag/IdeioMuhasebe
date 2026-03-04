using System;
using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Entities
{
    public class RecurringDebtSkip
    {
        public int Id { get; set; }

        [Required]
        public int RecurringDebtId { get; set; }
        public RecurringDebt RecurringDebt { get; set; }

        // Ay bazlı tutulur: her zaman ayın 1'i (2026-03-01 gibi)
        [Required]
        public DateTime Month { get; set; }

        public DateTime CreateDate { get; set; } = DateTime.Now;
    }
}