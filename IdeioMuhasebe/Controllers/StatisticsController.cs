using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Entities;
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
            string expenseKind = "total" // total | normal | tax_total | tax_debt | tax_income
        )
        {
            var (f, toEx) = Range(from, to);
            var months = MonthsBetween(f, toEx);

            expenseStatus = (expenseStatus ?? "total").ToLowerInvariant();
            incomeStatus = (incomeStatus ?? "total").ToLowerInvariant();
            expenseKind = (expenseKind ?? "total").ToLowerInvariant();
            mode = (mode ?? "expense").ToLowerInvariant();

            // Backward compatibility: eski "tax" değeri gelirse "tax_total" say
            if (expenseKind == "tax") expenseKind = "tax_total";

            // ------------------------------------------------------------
            // DEBTS base query (status + category)
            // ------------------------------------------------------------
            IQueryable<Debt> debtQ = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                debtQ = debtQ.Where(x => x.DebtTypeId == debtTypeId.Value);

            if (expenseStatus == "paid") debtQ = debtQ.Where(x => x.IsPaid);
            else if (expenseStatus == "unpaid") debtQ = debtQ.Where(x => !x.IsPaid);

            // ✅ Normal gider = Debt.NetAmount (eski kayıt uyumu: Net=0 & Tax=0 => Amount)
            var normalMonthlyMap = await debtQ
                .GroupBy(x => new { x.DueDate.Year, x.DueDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => (x.NetAmount == 0m && x.TaxAmount == 0m) ? x.Amount : x.NetAmount)
                })
                .ToListAsync();

            var normalMonthlyDict = normalMonthlyMap.ToDictionary(
                x => $"{x.Year:D4}-{x.Month:D2}",
                x => x.Total
            );

            var normalByType = await debtQ
                .GroupBy(x => x.DebtType.Name)
                .Select(g => new
                {
                    type = g.Key,
                    total = g.Sum(x => (x.NetAmount == 0m && x.TaxAmount == 0m) ? x.Amount : x.NetAmount)
                })
                .OrderByDescending(x => x.total)
                .ToListAsync();

            // ✅ Gider Vergisi = Debt.TaxAmount
            var debtTaxMonthlyMap = await debtQ
                .GroupBy(x => new { x.DueDate.Year, x.DueDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.TaxAmount) })
                .ToListAsync();

            var debtTaxMonthlyDict = debtTaxMonthlyMap.ToDictionary(
                x => $"{x.Year:D4}-{x.Month:D2}",
                x => x.Total
            );

            var debtTaxTotal = debtTaxMonthlyMap.Sum(x => x.Total);

            // ------------------------------------------------------------
            // Gelir Vergisi (Income.TaxAmount) gider sayılır
            // incomeStatus + incomeTypeId burada uygulanır
            // ------------------------------------------------------------
            IQueryable<Income> incomeTaxQ = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                incomeTaxQ = incomeTaxQ.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            if (incomeStatus == "paid") incomeTaxQ = incomeTaxQ.Where(x => x.IsReceived);
            else if (incomeStatus == "unpaid") incomeTaxQ = incomeTaxQ.Where(x => !x.IsReceived);

            var incomeTaxMonthlyMap = await incomeTaxQ
                .GroupBy(x => new { x.DueDate.Year, x.DueDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.TaxAmount) })
                .ToListAsync();

            var incomeTaxMonthlyDict = incomeTaxMonthlyMap.ToDictionary(
                x => $"{x.Year:D4}-{x.Month:D2}",
                x => x.Total
            );

            var incomeTaxTotal = incomeTaxMonthlyMap.Sum(x => x.Total);

            decimal GetDebtTax(string key) => debtTaxMonthlyDict.TryGetValue(key, out var v) ? v : 0m;
            decimal GetIncomeTax(string key) => incomeTaxMonthlyDict.TryGetValue(key, out var v) ? v : 0m;

            decimal GetSelectedTax(string key)
            {
                var dt = GetDebtTax(key);
                var it = GetIncomeTax(key);

                return expenseKind switch
                {
                    "tax_debt" => dt,
                    "tax_income" => it,
                    "tax_total" => dt + it,
                    _ => dt + it
                };
            }

            decimal SelectedTaxTotal()
            {
                return expenseKind switch
                {
                    "tax_debt" => debtTaxTotal,
                    "tax_income" => incomeTaxTotal,
                    "tax_total" => debtTaxTotal + incomeTaxTotal,
                    _ => debtTaxTotal + incomeTaxTotal
                };
            }

            // ------------------------------------------------------------
            // EXPENSE monthly + byType (expenseKind'e göre)
            // total: normal + (debtTax + incomeTax)
            // normal: normal
            // tax_debt / tax_income / tax_total: sadece seçilen vergi
            // ------------------------------------------------------------
            var expenseMonthly = months.Select(m =>
            {
                var key = $"{m.Year:D4}-{m.Month:D2}";
                var normal = normalMonthlyDict.TryGetValue(key, out var nv) ? nv : 0m;

                var fullTax = GetDebtTax(key) + GetIncomeTax(key);
                var selectedTax = GetSelectedTax(key);

                decimal v = expenseKind switch
                {
                    "normal" => normal,
                    "tax_debt" => selectedTax,
                    "tax_income" => selectedTax,
                    "tax_total" => selectedTax,
                    _ => normal + fullTax // total
                };

                return (object)new { month = key, total = v };
            }).ToList();

            List<object> expenseByType;

            if (expenseKind == "normal")
            {
                expenseByType = normalByType.Cast<object>().ToList();
            }
            else if (expenseKind == "tax_debt")
            {
                expenseByType = new List<object>();
                if (debtTaxTotal > 0) expenseByType.Add(new { type = "Gider Vergisi", total = debtTaxTotal });
            }
            else if (expenseKind == "tax_income")
            {
                expenseByType = new List<object>();
                if (incomeTaxTotal > 0) expenseByType.Add(new { type = "Gelir Vergisi", total = incomeTaxTotal });
            }
            else if (expenseKind == "tax_total")
            {
                expenseByType = new List<object>();
                if (debtTaxTotal > 0) expenseByType.Add(new { type = "Gider Vergisi", total = debtTaxTotal });
                if (incomeTaxTotal > 0) expenseByType.Add(new { type = "Gelir Vergisi", total = incomeTaxTotal });
            }
            else
            {
                // total: normal by type + iki vergi slice
                expenseByType = normalByType.Cast<object>().ToList();
                if (debtTaxTotal > 0) expenseByType.Add(new { type = "Gider Vergisi", total = debtTaxTotal });
                if (incomeTaxTotal > 0) expenseByType.Add(new { type = "Gelir Vergisi", total = incomeTaxTotal });
            }

            // ------------------------------------------------------------
            // INCOME (✅ TOPLAM GELİR = Amount)
            // ------------------------------------------------------------
            IQueryable<Income> incomeQ = _db.Incomes.AsNoTracking()
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

            return Json(new { ok = true, mode = "expense", monthly = expenseMonthly, byType = expenseByType });
        }
    }
}