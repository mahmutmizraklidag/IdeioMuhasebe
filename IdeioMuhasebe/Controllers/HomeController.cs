using IdeioMuhasebe.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly DatabaseContext _db;
        public HomeController(DatabaseContext db) => _db = db;

        public IActionResult Index() => View();

        private static (DateTime from, DateTime toExclusive) Range(DateTime? from, DateTime? to)
        {
            var f = (from ?? DateTime.Today).Date;
            var t = (to ?? DateTime.Today).Date;
            if (t < f) (f, t) = (t, f);
            return (f, t.AddDays(1));
        }

        private static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
        private static int MonthDiff(DateTime aMonth, DateTime bMonth)
            => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

        // Dashboard: kartlar + yaklaşan giderler + yaklaşan gelirler (seçili aralığa göre)
        [HttpGet]
        public async Task<IActionResult> Summary(DateTime? from, DateTime? to, int? debtTypeId, int? incomeTypeId)
        {
            var (f, toEx) = Range(from, to);

            decimal Clamp(decimal v, decimal max) => v < 0 ? 0m : (v > max ? max : v);

            // ---------------------------------------------------------
            // EXPENSES (Debts)
            // ---------------------------------------------------------
            var qExp = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Include(x => x.RecurringDebt)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                qExp = qExp.Where(x => x.DebtTypeId == debtTypeId.Value);

            var expRawAll = await qExp
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Amount,
                    x.PaidAmount,
                    x.DueDate,
                    debtType = x.DebtType.Name,
                    debtTypeId = x.DebtTypeId,
                    x.IsPaid,
                    recurringDebtId = x.RecurringDebtId,
                    recurringStartDate = x.RecurringDebt != null ? (DateTime?)x.RecurringDebt.StartDate : null,
                    recurringPeriodCount = x.RecurringDebt != null ? x.RecurringDebt.PeriodCount : null
                })
                .ToListAsync();

            var debtTotal = expRawAll.Sum(x => x.Amount);
            var debtPaid = expRawAll.Sum(x => Clamp(x.PaidAmount ?? 0, x.Amount));

            var debtUnpaid = expRawAll.Sum(x => (x.Amount) - Clamp(x.PaidAmount ?? 0, x.Amount));

            var expRaw = expRawAll
                .Where(x => !x.IsPaid)
                .OrderBy(x => x.DueDate)
                .Take(50)
                .ToList();

            // ---------------------------------------------------------
            // INCOMES
            // ---------------------------------------------------------
            var qInc = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Include(x => x.RecurringIncome)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                qInc = qInc.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            var incRawAll = await qInc
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Amount,
                    x.TaxAmount,
                    x.ReceivedAmount,
                    x.DueDate,
                    incomeType = x.IncomeType.Name,
                    incomeTypeId = x.IncomeTypeId,
                    x.IsReceived,
                    recurringIncomeId = x.RecurringIncomeId,
                    recurringStartDate = x.RecurringIncome != null ? (DateTime?)x.RecurringIncome.StartDate : null,
                    recurringPeriodCount = x.RecurringIncome != null ? x.RecurringIncome.PeriodCount : null
                })
                .ToListAsync();

            var incTotal = incRawAll.Sum(x => x.Amount);
            var incReceived = incRawAll.Sum(x => Clamp(x.ReceivedAmount, x.Amount));
            var incRemaining = incRawAll.Sum(x => x.Amount - Clamp(x.ReceivedAmount, x.Amount));

            // Gelir vergisi gider sayılacak ama kısmi tahsilata göre oranlanacak
            decimal IncomeTaxReceived(dynamic x)
            {
                var received = Clamp((decimal)x.ReceivedAmount, (decimal)x.Amount);
                if (x.Amount <= 0) return 0m;
                return x.TaxAmount * (received / x.Amount);
            }

            decimal IncomeTaxRemaining(dynamic x) => x.TaxAmount - IncomeTaxReceived(x);

            var incTaxTotal = incRawAll.Sum(x => x.TaxAmount);
            var incTaxReceived = incRawAll.Sum(x => IncomeTaxReceived(x));
            var incTaxRemaining = incRawAll.Sum(x => IncomeTaxRemaining(x));

            var incRaw = incRawAll
                .Where(x => !x.IsReceived)
                .OrderBy(x => x.DueDate)
                .Take(50)
                .ToList();

            // ---------------------------------------------------------
            // recurring period text
            // ---------------------------------------------------------
            static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
            static int MonthDiff(DateTime aMonth, DateTime bMonth)
                => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

            var upcomingExpenses = expRaw.Select(x =>
            {
                string? periodText = null;

                if (x.recurringDebtId.HasValue &&
                    x.recurringStartDate.HasValue &&
                    x.recurringPeriodCount.HasValue &&
                    x.recurringPeriodCount.Value > 0)
                {
                    var startM = MonthStart(x.recurringStartDate.Value);
                    var dueM = MonthStart(x.DueDate);
                    var idx = MonthDiff(startM, dueM) + 1;

                    if (idx < 1) idx = 1;
                    if (idx > x.recurringPeriodCount.Value) idx = x.recurringPeriodCount.Value;

                    periodText = $"{idx}/{x.recurringPeriodCount.Value}";
                }

                var paid = Clamp(x.PaidAmount ?? 0, x.Amount);
                var remaining = x.Amount - paid;

                return new
                {
                    x.Id,
                    x.Name,
                    amount = remaining,
                    totalAmount = x.Amount,
                    paidAmount = paid,
                    remainingAmount = remaining,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    x.debtType,
                    x.debtTypeId,
                    x.IsPaid,
                    recurringPeriodText = periodText
                };
            }).ToList();

            var upcomingIncomes = incRaw.Select(x =>
            {
                string? periodText = null;

                if (x.recurringIncomeId.HasValue &&
                    x.recurringStartDate.HasValue &&
                    x.recurringPeriodCount.HasValue &&
                    x.recurringPeriodCount.Value > 0)
                {
                    var startM = MonthStart(x.recurringStartDate.Value);
                    var dueM = MonthStart(x.DueDate);
                    var idx = MonthDiff(startM, dueM) + 1;

                    if (idx < 1) idx = 1;
                    if (idx > x.recurringPeriodCount.Value) idx = x.recurringPeriodCount.Value;

                    periodText = $"{idx}/{x.recurringPeriodCount.Value}";
                }

                var received = Clamp(x.ReceivedAmount, x.Amount);
                var remaining = x.Amount - received;

                return new
                {
                    x.Id,
                    x.Name,
                    amount = remaining,
                    totalAmount = x.Amount,
                    receivedAmount = received,
                    remainingAmount = remaining,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    x.incomeType,
                    x.incomeTypeId,
                    x.IsReceived,
                    recurringPeriodText = periodText
                };
            }).ToList();

            // ---------------------------------------------------------
            // HOME KARTLARI
            // gider = borç toplamı + gelir vergisi
            // ama paid/remaining kısmi mantıkla
            // ---------------------------------------------------------
            var expTotal = debtTotal + incTaxTotal;
            var expPaid = debtPaid + incTaxReceived;
            var expRemaining = debtUnpaid + incTaxRemaining;

            return Json(new
            {
                ok = true,
                range = new
                {
                    from = f.ToString("yyyy-MM-dd"),
                    to = toEx.AddDays(-1).ToString("yyyy-MM-dd")
                },
                cards = new
                {
                    expenseTotal = expTotal,
                    expensePaid = expPaid,
                    expenseRemaining = expRemaining,
                    incomeTotal = incTotal,
                    incomeReceived = incReceived,
                    incomeRemaining = incRemaining
                },
                upcomingExpenses,
                upcomingIncomes
            });
        }
    }
}