using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Services
{
    public class RecurringDebtService
    {
        private readonly DatabaseContext _db;

        public RecurringDebtService(DatabaseContext db)
        {
            _db = db;
        }

        private static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);

        private static int ClampDay(int year, int month, int day)
        {
            var max = DateTime.DaysInMonth(year, month);
            if (day < 1) return 1;
            if (day > max) return max;
            return day;
        }

        public async Task EnsureGeneratedAsync(DateTime today)
        {
            var targetMonth = MonthStart(today);

            var rules = await _db.RecurringDebts.AsNoTracking()
                .Where(r => r.IsActive)
                .ToListAsync();

            foreach (var r in rules)
            {
                var startMonth = MonthStart(r.StartDate);

                DateTime endMonth = DateTime.MaxValue;
                if (r.PeriodCount.HasValue && r.PeriodCount.Value > 0)
                    endMonth = startMonth.AddMonths(r.PeriodCount.Value - 1);

                var lastMonth = targetMonth;
                if (endMonth < lastMonth) lastMonth = endMonth;

                if (lastMonth < startMonth) continue;

                var fromDate = startMonth;
                var toEx = lastMonth.AddMonths(1);

                // ✅ 1) Bu rule için mevcut borçların dueDate’leri
                var existingDates = await _db.Debts.AsNoTracking()
                    .Where(x => x.RecurringDebtId == r.Id && x.DueDate >= fromDate && x.DueDate < toEx)
                    .Select(x => x.DueDate)
                    .ToListAsync();

                var existingSet = new HashSet<DateTime>(existingDates);

                // ✅ 2) Bu rule için “skip edilen aylar”
                var skipMonths = await _db.RecurringDebtSkips.AsNoTracking()
                    .Where(s => s.RecurringDebtId == r.Id && s.Month >= startMonth && s.Month < toEx)
                    .Select(s => s.Month)
                    .ToListAsync();

                var skipSet = new HashSet<DateTime>(skipMonths);

                for (var m = startMonth; m <= lastMonth; m = m.AddMonths(1))
                {
                    // ✅ skip varsa o ayı hiç üretme
                    if (skipSet.Contains(m)) continue;

                    var day = ClampDay(m.Year, m.Month, r.DayOfMonth);
                    var due = new DateTime(m.Year, m.Month, day);

                    if (existingSet.Contains(due)) continue;

                    _db.Debts.Add(new Debt
                    {
                        DebtTypeId = r.DebtTypeId,
                        Name = r.Name,
                        Amount = r.Amount,
                        DueDate = due.Date,
                        Payee = r.Payee,
                        IsPaid = false,
                        UpdatedDate = DateTime.Now,
                        RecurringDebtId = r.Id
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}