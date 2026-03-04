using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Services
{
    public class RecurringIncomeService
    {
        private readonly DatabaseContext _db;

        public RecurringIncomeService(DatabaseContext db)
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

            var rules = await _db.RecurringIncomes.AsNoTracking()
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

                var existingDates = await _db.Incomes.AsNoTracking()
                    .Where(x => x.RecurringIncomeId == r.Id && x.DueDate >= fromDate && x.DueDate < toEx)
                    .Select(x => x.DueDate)
                    .ToListAsync();

                var existingSet = new HashSet<DateTime>(existingDates);

                var skipMonths = await _db.RecurringIncomeSkips.AsNoTracking()
                    .Where(s => s.RecurringIncomeId == r.Id && s.Month >= startMonth && s.Month < toEx)
                    .Select(s => s.Month)
                    .ToListAsync();

                var skipSet = new HashSet<DateTime>(skipMonths);

                for (var m = startMonth; m <= lastMonth; m = m.AddMonths(1))
                {
                    if (skipSet.Contains(m)) continue;

                    var day = ClampDay(m.Year, m.Month, r.DayOfMonth);
                    var due = new DateTime(m.Year, m.Month, day);

                    if (existingSet.Contains(due)) continue;

                    _db.Incomes.Add(new Income
                    {
                        IncomeTypeId = r.IncomeTypeId,
                        Name = r.Name,
                        NetAmount = r.NetAmount,
                        TaxAmount = r.TaxAmount,
                        Amount = r.NetAmount + r.TaxAmount,
                        DueDate = due.Date,
                        Payer = r.Payer,
                        IsReceived = false,
                        UpdatedDate = DateTime.Now,
                        RecurringIncomeId = r.Id
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}