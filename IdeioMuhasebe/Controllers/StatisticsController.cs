using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class StatisticsController : Controller
    {
        private readonly DatabaseContext _db;

        public StatisticsController(DatabaseContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index() => View();

        private static (DateTime from, DateTime toExclusive) Range(DateTime? from, DateTime? to)
        {
            // tarih gelmezse bu ay
            var now = DateTime.Today;
            var defaultFrom = new DateTime(now.Year, now.Month, 1);
            var defaultToEx = defaultFrom.AddMonths(1);

            if (from == null || to == null) return (defaultFrom, defaultToEx);

            var f = from.Value.Date;
            var t = to.Value.Date;
            if (t < f) (f, t) = (t, f);
            return (f, t.AddDays(1));
        }

        private static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);

        private static List<DateTime> MonthsBetween(DateTime fromDate, DateTime toExclusive)
        {
            var list = new List<DateTime>();
            var cur = MonthStart(fromDate);
            var last = MonthStart(toExclusive.AddDays(-1));
            while (cur <= last)
            {
                list.Add(cur);
                cur = cur.AddMonths(1);
            }
            return list;
        }

        [HttpGet]
        public async Task<IActionResult> DataV2(
     DateTime? from,
     DateTime? to,
     string mode = "expense",
     int? debtTypeId = null,
     int? incomeTypeId = null,
     string expenseStatus = "total",
     string incomeStatus = "total",
     string expenseKind = "total" // total | normal | tax
 )
        {
            var (f, toEx) = Range(from, to);

            static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);

            static List<DateTime> MonthsBetween(DateTime fromDate, DateTime toExclusive)
            {
                var list = new List<DateTime>();
                var cur = MonthStart(fromDate);
                var last = MonthStart(toExclusive.AddDays(-1));
                while (cur <= last)
                {
                    list.Add(cur);
                    cur = cur.AddMonths(1);
                }
                return list;
            }

            var months = MonthsBetween(f, toEx);

            expenseStatus = (expenseStatus ?? "total").ToLowerInvariant();
            incomeStatus = (incomeStatus ?? "total").ToLowerInvariant();
            expenseKind = (expenseKind ?? "total").ToLowerInvariant();
            mode = (mode ?? "expense").ToLowerInvariant();

            // ------------------------------------------------------------
            // NORMAL EXPENSE (Debts)
            // ------------------------------------------------------------
            IQueryable<IdeioMuhasebe.Entities.Debt> debtQ = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                debtQ = debtQ.Where(x => x.DebtTypeId == debtTypeId.Value);

            if (expenseStatus == "paid") debtQ = debtQ.Where(x => x.IsPaid);
            else if (expenseStatus == "unpaid") debtQ = debtQ.Where(x => !x.IsPaid);

            var normalMonthlyMap = await debtQ
                .GroupBy(x => new { x.DueDate.Year, x.DueDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var normalMonthlyDict = normalMonthlyMap.ToDictionary(
                x => $"{x.Year:D4}-{x.Month:D2}",
                x => x.Total
            );

            var normalByType = await debtQ
                .GroupBy(x => x.DebtType.Name)
                .Select(g => new { type = g.Key, total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.total)
                .ToListAsync();

            // ------------------------------------------------------------
            // TAX EXPENSE (Income.TaxAmount)
            // (incomeStatus + incomeTypeId burada da uygulanır)
            // ------------------------------------------------------------
            IQueryable<IdeioMuhasebe.Entities.Income> taxQ = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                taxQ = taxQ.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            if (incomeStatus == "paid") taxQ = taxQ.Where(x => x.IsReceived);
            else if (incomeStatus == "unpaid") taxQ = taxQ.Where(x => !x.IsReceived);

            var taxMonthlyMap = await taxQ
                .GroupBy(x => new { x.DueDate.Year, x.DueDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.TaxAmount) })
                .ToListAsync();

            var taxMonthlyDict = taxMonthlyMap.ToDictionary(
                x => $"{x.Year:D4}-{x.Month:D2}",
                x => x.Total
            );

            var taxTotal = taxMonthlyMap.Sum(x => x.Total);

            // ------------------------------------------------------------
            // EXPENSE monthly + byType (expenseKind'e göre)
            // ------------------------------------------------------------
            var expenseMonthly = months.Select(m =>
            {
                var key = $"{m.Year:D4}-{m.Month:D2}";
                var normal = normalMonthlyDict.TryGetValue(key, out var nv) ? nv : 0m;
                var tax = taxMonthlyDict.TryGetValue(key, out var tv) ? tv : 0m;

                decimal v =
                    expenseKind == "normal" ? normal :
                    expenseKind == "tax" ? tax :
                    (normal + tax); // total

                return (object)new { month = key, total = v };
            }).ToList();

            List<object> expenseByType;
            if (expenseKind == "tax")
            {
                expenseByType = new List<object>();
                if (taxTotal > 0)
                    expenseByType.Add(new { type = "Vergi", total = taxTotal });
            }
            else if (expenseKind == "normal")
            {
                expenseByType = normalByType.Cast<object>().ToList();
            }
            else
            {
                expenseByType = normalByType.Cast<object>().ToList();
                if (taxTotal > 0)
                    expenseByType.Add(new { type = "Vergi", total = taxTotal });
            }

            // ------------------------------------------------------------
            // INCOME (✅ TOPLAM GELİR = Amount)
            // ------------------------------------------------------------
            IQueryable<IdeioMuhasebe.Entities.Income> incomeQ = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                incomeQ = incomeQ.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            if (incomeStatus == "paid") incomeQ = incomeQ.Where(x => x.IsReceived);
            else if (incomeStatus == "unpaid") incomeQ = incomeQ.Where(x => !x.IsReceived);

            var incomeMonthlyMap = await incomeQ
                .GroupBy(x => new { x.DueDate.Year, x.DueDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => x.Amount)
                })
                .ToListAsync();

            var incomeMonthlyDict = incomeMonthlyMap.ToDictionary(
                x => $"{x.Year:D4}-{x.Month:D2}",
                x => x.Total
            );

            var incomeMonthly = months.Select(m =>
            {
                var key = $"{m.Year:D4}-{m.Month:D2}";
                return (object)new
                {
                    month = key,
                    total = incomeMonthlyDict.TryGetValue(key, out var v) ? v : 0m
                };
            }).ToList();

            var incomeByType = await incomeQ
                .GroupBy(x => x.IncomeType.Name)
                .Select(g => new
                {
                    type = g.Key,
                    total = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.total)
                .Cast<object>()
                .ToListAsync();

            // ------------------------------------------------------------
            // RESPONSE
            // ------------------------------------------------------------
            if (mode == "income")
            {
                return Json(new { ok = true, mode, monthly = incomeMonthly, byType = incomeByType });
            }

            if (mode == "compare")
            {
                return Json(new
                {
                    ok = true,
                    mode,
                    expense = new { monthly = expenseMonthly, byType = expenseByType },
                    income = new { monthly = incomeMonthly, byType = incomeByType }
                });
            }

            // default: expense
            return Json(new { ok = true, mode = "expense", monthly = expenseMonthly, byType = expenseByType });
        }
    }
}