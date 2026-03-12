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

        // =========================================================
        // EXISTING: DataV2 (aylık çizgi + donut + compare)
        // =========================================================
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

            if (expenseKind == "tax") expenseKind = "tax_total";

            var debtQ = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                debtQ = debtQ.Where(x => x.DebtTypeId == debtTypeId.Value);

            var incomeQ = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                incomeQ = incomeQ.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            var debts = await debtQ
                .Select(x => new DebtStatRow
                {
                    DueDate = x.DueDate,
                    Type = x.DebtType.Name,
                    Amount = x.Amount,
                    NetAmount = x.NetAmount,
                    TaxAmount = x.TaxAmount,
                    PaidAmount = x.PaidAmount ?? 0
                })
                .ToListAsync();

            var incomes = await incomeQ
                .Select(x => new IncomeStatRow
                {
                    DueDate = x.DueDate,
                    Type = x.IncomeType.Name,
                    Amount = x.Amount,
                    TaxAmount = x.TaxAmount,
                    ReceivedAmount = x.ReceivedAmount
                })
                .ToListAsync();

            var expenseMonthly = months.Select(m =>
            {
                var debtMonth = debts.Where(x => MonthStart(x.DueDate) == m).ToList();
                var incomeMonth = incomes.Where(x => MonthStart(x.DueDate) == m).ToList();

                var normal = debtMonth.Sum(x => DebtNormalByStatus(x, expenseStatus));
                var debtTax = debtMonth.Sum(x => DebtTaxByStatus(x, expenseStatus));
                var incomeTax = incomeMonth.Sum(x => IncomeTaxByStatus(x, incomeStatus));

                decimal total = expenseKind switch
                {
                    "normal" => normal,
                    "tax_debt" => debtTax,
                    "tax_income" => incomeTax,
                    "tax_total" => debtTax + incomeTax,
                    _ => normal + debtTax + incomeTax
                };

                return (object)new
                {
                    month = $"{m.Year:D4}-{m.Month:D2}",
                    total
                };
            }).ToList();

            List<object> expenseByType;
            if (expenseKind == "normal")
            {
                expenseByType = debts
                    .GroupBy(x => x.Type)
                    .Select(g => new
                    {
                        type = g.Key,
                        total = g.Sum(x => DebtNormalByStatus(x, expenseStatus))
                    })
                    .Where(x => x.total > 0)
                    .OrderByDescending(x => x.total)
                    .Cast<object>()
                    .ToList();
            }
            else if (expenseKind == "tax_debt")
            {
                var v = debts.Sum(x => DebtTaxByStatus(x, expenseStatus));
                expenseByType = new List<object>();
                if (v > 0) expenseByType.Add(new { type = "Gider Vergisi", total = v });
            }
            else if (expenseKind == "tax_income")
            {
                var v = incomes.Sum(x => IncomeTaxByStatus(x, incomeStatus));
                expenseByType = new List<object>();
                if (v > 0) expenseByType.Add(new { type = "Gelir Vergisi", total = v });
            }
            else if (expenseKind == "tax_total")
            {
                var debtTax = debts.Sum(x => DebtTaxByStatus(x, expenseStatus));
                var incomeTax = incomes.Sum(x => IncomeTaxByStatus(x, incomeStatus));

                expenseByType = new List<object>();
                if (debtTax > 0) expenseByType.Add(new { type = "Gider Vergisi", total = debtTax });
                if (incomeTax > 0) expenseByType.Add(new { type = "Gelir Vergisi", total = incomeTax });
            }
            else
            {
                expenseByType = debts
                    .GroupBy(x => x.Type)
                    .Select(g => new
                    {
                        type = g.Key,
                        total = g.Sum(x => DebtNormalByStatus(x, expenseStatus))
                    })
                    .Where(x => x.total > 0)
                    .OrderByDescending(x => x.total)
                    .Cast<object>()
                    .ToList();

                var debtTax = debts.Sum(x => DebtTaxByStatus(x, expenseStatus));
                var incomeTax = incomes.Sum(x => IncomeTaxByStatus(x, incomeStatus));

                if (debtTax > 0) expenseByType.Add(new { type = "Gider Vergisi", total = debtTax });
                if (incomeTax > 0) expenseByType.Add(new { type = "Gelir Vergisi", total = incomeTax });
            }

            var incomeMonthly = months.Select(m =>
            {
                var monthRows = incomes.Where(x => MonthStart(x.DueDate) == m).ToList();

                return (object)new
                {
                    month = $"{m.Year:D4}-{m.Month:D2}",
                    total = monthRows.Sum(x => IncomeTotalByStatus(x, incomeStatus))
                };
            }).ToList();

            var incomeByType = incomes
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    type = g.Key,
                    total = g.Sum(x => IncomeTotalByStatus(x, incomeStatus))
                })
                .Where(x => x.total > 0)
                .OrderByDescending(x => x.total)
                .Cast<object>()
                .ToList();

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
        // =========================================================
        // NEW: CompareBar (2 sütun - kullanıcı istediği A/B'yi karşılaştırır)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> CompareBar(
       [FromQuery] DateTime? aFrom,
       [FromQuery] DateTime? aTo,
       [FromQuery] string aMetric = "income_total",
       [FromQuery] int? aDebtTypeId = null,
       [FromQuery] int? aIncomeTypeId = null,
       [FromQuery] string aExpenseStatus = "total",
       [FromQuery] string aIncomeStatus = "total",

       [FromQuery] DateTime? bFrom = null,
       [FromQuery] DateTime? bTo = null,
       [FromQuery] string bMetric = "expense_total",
       [FromQuery] int? bDebtTypeId = null,
       [FromQuery] int? bIncomeTypeId = null,
       [FromQuery] string bExpenseStatus = "total",
       [FromQuery] string bIncomeStatus = "total"
   )
        {
            aMetric = (aMetric ?? "income_total").ToLowerInvariant();
            bMetric = (bMetric ?? "expense_total").ToLowerInvariant();

            aExpenseStatus = (aExpenseStatus ?? "total").ToLowerInvariant();
            aIncomeStatus = (aIncomeStatus ?? "total").ToLowerInvariant();
            bExpenseStatus = (bExpenseStatus ?? "total").ToLowerInvariant();
            bIncomeStatus = (bIncomeStatus ?? "total").ToLowerInvariant();

            if (aMetric == "tax") aMetric = "tax_total";
            if (bMetric == "tax") bMetric = "tax_total";

            var a = await ComputeMetricTotal(aFrom, aTo, aMetric, aDebtTypeId, aIncomeTypeId, aExpenseStatus, aIncomeStatus);
            var b = await ComputeMetricTotal(bFrom, bTo, bMetric, bDebtTypeId, bIncomeTypeId, bExpenseStatus, bIncomeStatus);

            return Json(new { ok = true, a, b });
        }

        private async Task<decimal> ComputeMetricTotal(
            DateTime? from,
            DateTime? to,
            string metric,
            int? debtTypeId,
            int? incomeTypeId,
            string expenseStatus,
            string incomeStatus
        )
        {
            var (f, toEx) = Range(from, to);

            var debtQ = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                debtQ = debtQ.Where(x => x.DebtTypeId == debtTypeId.Value);

            var incomeQ = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                incomeQ = incomeQ.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            var debts = await debtQ
                .Select(x => new DebtStatRow
                {
                    DueDate = x.DueDate,
                    Type = x.DebtType.Name,
                    Amount = x.Amount,
                    NetAmount = x.NetAmount,
                    TaxAmount = x.TaxAmount,
                    PaidAmount = x.PaidAmount ?? 0
                })
                .ToListAsync();

            var incomes = await incomeQ
                .Select(x => new IncomeStatRow
                {
                    DueDate = x.DueDate,
                    Type = x.IncomeType.Name,
                    Amount = x.Amount,
                    TaxAmount = x.TaxAmount,
                    ReceivedAmount = x.ReceivedAmount
                })
                .ToListAsync();

            return metric switch
            {
                "income_total" => incomes.Sum(x => IncomeTotalByStatus(x, incomeStatus)),
                "expense_normal" => debts.Sum(x => DebtNormalByStatus(x, expenseStatus)),
                "tax_debt" => debts.Sum(x => DebtTaxByStatus(x, expenseStatus)),
                "tax_income" => incomes.Sum(x => IncomeTaxByStatus(x, incomeStatus)),
                "tax_total" => debts.Sum(x => DebtTaxByStatus(x, expenseStatus)) + incomes.Sum(x => IncomeTaxByStatus(x, incomeStatus)),
                "expense_total" =>
                    debts.Sum(x => DebtNormalByStatus(x, expenseStatus)) +
                    debts.Sum(x => DebtTaxByStatus(x, expenseStatus)) +
                    incomes.Sum(x => IncomeTaxByStatus(x, incomeStatus)),
                _ => incomes.Sum(x => IncomeTotalByStatus(x, incomeStatus))
            };
        }
        private class DebtStatRow
        {
            public DateTime DueDate { get; set; }
            public string Type { get; set; }
            public decimal Amount { get; set; }
            public decimal NetAmount { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal PaidAmount { get; set; }
        }

        private class IncomeStatRow
        {
            public DateTime DueDate { get; set; }
            public string Type { get; set; }
            public decimal Amount { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal ReceivedAmount { get; set; }
        }

        private static decimal Clamp(decimal value, decimal max)
        {
            if (value < 0) return 0m;
            if (value > max) return max;
            return value;
        }

        private static decimal DebtNormalTotal(DebtStatRow x)
        {
            return (x.NetAmount == 0m && x.TaxAmount == 0m) ? x.Amount : x.NetAmount;
        }

        private static decimal DebtPaidRatio(DebtStatRow x)
        {
            if (x.Amount <= 0) return 0m;
            return Clamp(x.PaidAmount, x.Amount) / x.Amount;
        }

        private static decimal DebtNormalByStatus(DebtStatRow x, string status)
        {
            var total = DebtNormalTotal(x);
            var paid = total * DebtPaidRatio(x);

            return status switch
            {
                "paid" => paid,
                "unpaid" => total - paid,
                _ => total
            };
        }

        private static decimal DebtTaxByStatus(DebtStatRow x, string status)
        {
            var total = x.TaxAmount;
            var paid = total * DebtPaidRatio(x);

            return status switch
            {
                "paid" => paid,
                "unpaid" => total - paid,
                _ => total
            };
        }

        private static decimal IncomeTotalByStatus(IncomeStatRow x, string status)
        {
            var paid = Clamp(x.ReceivedAmount, x.Amount);

            return status switch
            {
                "paid" => paid,
                "unpaid" => x.Amount - paid,
                _ => x.Amount
            };
        }

        private static decimal IncomeTaxByStatus(IncomeStatRow x, string status)
        {
            if (x.Amount <= 0)
                return status == "total" ? x.TaxAmount : 0m;

            var paidRatio = Clamp(x.ReceivedAmount, x.Amount) / x.Amount;
            var paidTax = x.TaxAmount * paidRatio;

            return status switch
            {
                "paid" => paidTax,
                "unpaid" => x.TaxAmount - paidTax,
                _ => x.TaxAmount
            };
        }
    }
}