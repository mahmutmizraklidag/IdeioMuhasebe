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

        private static string YmKey(DateTime m) => $"{m.Year:D4}-{m.Month:D2}";
        private static string YmdKey(DateTime d) => d.ToString("yyyy-MM-dd");

        private static (decimal net, decimal tax) DistributeAmounts(decimal baseNet, decimal baseTax, decimal baseTotal, decimal newTotal)
        {
            if (newTotal <= 0)
                return (0m, 0m);

            if (baseTotal <= 0)
                return (newTotal, 0m);

            if (baseNet <= 0 && baseTax <= 0)
                return (newTotal, 0m);

            if (baseNet <= 0)
                return (0m, newTotal);

            if (baseTax <= 0)
                return (newTotal, 0m);

            var netRatio = baseNet / baseTotal;
            var net = Math.Round(newTotal * netRatio, 2, MidpointRounding.AwayFromZero);
            var tax = newTotal - net;

            if (tax < 0)
            {
                tax = 0m;
                net = newTotal;
            }

            return (net, tax);
        }

        public async Task EnsureGeneratedAsync(DateTime today)
        {
            var targetMonth = MonthStart(today);

            var rules = await _db.RecurringIncomes
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Id)
                .ToListAsync();

            foreach (var r in rules)
            {
                var startMonth = MonthStart(r.StartDate);

                DateTime endMonth = DateTime.MaxValue;
                if (r.PeriodCount.HasValue && r.PeriodCount.Value > 0)
                    endMonth = startMonth.AddMonths(r.PeriodCount.Value - 1);

                var lastMonth = targetMonth;
                if (endMonth < lastMonth)
                    lastMonth = endMonth;

                if (lastMonth < startMonth)
                    continue;

                var toEx = lastMonth.AddMonths(1);

                // YENİ HALİ: && !x.IsDeleted eklendi
                var existingIncomes = await _db.Incomes
                    .AsNoTracking()
                    .Where(x => x.RecurringIncomeId == r.Id
                             && x.DueDate >= startMonth
                             && x.DueDate < toEx
                             && !x.IsDeleted) // <-- EKLENEN KISIM
                    .Select(x => new
                    {
                        x.Id,
                        x.DueDate,
                        x.Amount,
                        x.ReceivedAmount,
                        x.CarryForwardBalance
                    })
                    .ToListAsync();

                var existingByDue = existingIncomes
                    .GroupBy(x => YmdKey(x.DueDate))
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(x => x.Id).First()
                    );

                var skipMonthKeys = await _db.RecurringIncomeSkips
                    .AsNoTracking()
                    .Where(s => s.RecurringIncomeId == r.Id && s.Month >= startMonth && s.Month < toEx)
                    .Select(s => s.Month)
                    .ToListAsync();

                var skipSet = new HashSet<string>(skipMonthKeys.Select(YmKey));

                decimal carry = 0m;

                for (var m = startMonth; m <= lastMonth; m = m.AddMonths(1))
                {
                    var ym = YmKey(m);

                    if (skipSet.Contains(ym))
                        continue;

                    var day = ClampDay(m.Year, m.Month, r.DayOfMonth);
                    var due = new DateTime(m.Year, m.Month, day);
                    var dueKey = YmdKey(due);

                    if (existingByDue.TryGetValue(dueKey, out var existing))
                    {
                        carry = existing.CarryForwardBalance;
                        continue;
                    }

                    var rawAmount = r.Amount + carry;

                    decimal newAmount = rawAmount <= 0m ? 0m : rawAmount;
                    decimal newReceivedAmount = 0m;
                    decimal newCarry = rawAmount <= 0m ? rawAmount : (newAmount - newReceivedAmount);
                    bool newIsReceived = newAmount == 0m;

                    var (newNet, newTax) = DistributeAmounts(r.NetAmount, r.TaxAmount, r.Amount, newAmount);

                    _db.Incomes.Add(new Income
                    {
                        IncomeTypeId = r.IncomeTypeId,
                        Name = r.Name,
                        NetAmount = newNet,
                        TaxAmount = newTax,
                        Amount = newAmount,
                        ReceivedAmount = newReceivedAmount,
                        CarryForwardBalance = newCarry,
                        DueDate = due.Date,
                        Payer = r.Payer,
                        IsReceived = newIsReceived,
                        UpdatedDate = DateTime.Now,
                        RecurringIncomeId = r.Id
                    });

                    carry = newCarry;
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}