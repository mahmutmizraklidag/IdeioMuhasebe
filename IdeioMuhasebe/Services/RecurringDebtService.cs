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

        private static string YmKey(DateTime m) => $"{m.Year:D4}-{m.Month:D2}";
        private static string YmdKey(DateTime d) => d.ToString("yyyy-MM-dd");

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

                var toEx = lastMonth.AddMonths(1);

                // 1) Bu rule için mevcut üretilen borçların dueDate key set'i
                var existingDueKeys = await _db.Debts.AsNoTracking()
                    .Where(x => x.RecurringDebtId == r.Id && x.DueDate >= startMonth && x.DueDate < toEx)
                    .Select(x => x.DueDate)
                    .ToListAsync();

                var existingSet = new HashSet<string>(existingDueKeys.Select(YmdKey));

                // 2) ✅ Bu rule için skip edilen ayların key set'i (yyyy-MM)
                var skipMonthKeys = await _db.RecurringDebtSkips.AsNoTracking()
                    .Where(s => s.RecurringDebtId == r.Id && s.Month >= startMonth && s.Month < toEx)
                    .Select(s => s.Month)
                    .ToListAsync();

                var skipSet = new HashSet<string>(skipMonthKeys.Select(YmKey));

                // 3) Üretim
                for (var m = startMonth; m <= lastMonth; m = m.AddMonths(1))
                {
                    var ym = YmKey(m);

                    // ✅ Bu ay skip ise ASLA üretme
                    if (skipSet.Contains(ym)) continue;

                    var day = ClampDay(m.Year, m.Month, r.DayOfMonth);
                    var due = new DateTime(m.Year, m.Month, day);

                    var dueKey = YmdKey(due);
                    if (existingSet.Contains(dueKey)) continue;

                    _db.Debts.Add(new Debt
                    {
                        DebtTypeId = r.DebtTypeId,
                        Name = r.Name,

                        NetAmount = r.NetAmount,
                        TaxAmount = r.TaxAmount,
                        Amount = r.NetAmount + r.TaxAmount,
                        PaidAmount = 0m,

                        DueDate = due.Date,
                        Payee = r.Payee,
                        IsPaid = false,
                        UpdatedDate = DateTime.Now,
                        RecurringDebtId = r.Id
                    });

                    // aynı run içinde bir daha üretmeyi engelle
                    existingSet.Add(dueKey);
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}