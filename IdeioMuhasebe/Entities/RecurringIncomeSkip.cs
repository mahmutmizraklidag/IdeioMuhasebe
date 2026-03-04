using System;
using System.ComponentModel.DataAnnotations;

namespace IdeioMuhasebe.Entities
{
    public class RecurringIncomeSkip
    {
        public int Id { get; set; }

        [Required]
        public int RecurringIncomeId { get; set; }
        public RecurringIncome RecurringIncome { get; set; }

        // Ay bazlı tutulur: her zaman ayın 1'i
        [Required]
        public DateTime Month { get; set; }

        public DateTime CreateDate { get; set; } = DateTime.Now;
    }
}