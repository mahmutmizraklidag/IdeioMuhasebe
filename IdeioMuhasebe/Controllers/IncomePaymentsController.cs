using System;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class IncomePaymentsController : Controller
    {
        private readonly DatabaseContext _db;
        private readonly RecurringIncomeService _recurring;

        public IncomePaymentsController(DatabaseContext db, RecurringIncomeService recurring)
        {
            _db = db;
            _recurring = recurring;
        }

        private static (DateTime from, DateTime toExclusive) DefaultMonthRange()
        {
            var now = DateTime.Today;
            var f = new DateTime(now.Year, now.Month, 1);
            return (f, f.AddMonths(1));
        }

        private static (DateTime from, DateTime toExclusive) Range(DateTime? from, DateTime? to)
        {
            if (from == null || to == null) return DefaultMonthRange();

            var f = from.Value.Date;
            var t = to.Value.Date;
            if (t < f) (f, t) = (t, f);
            return (f, t.AddDays(1));
        }

        private static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
        private static int MonthDiff(DateTime aMonth, DateTime bMonth)
            => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

        private static bool LastOnePeriodWarning(DateTime dueDate, DateTime startDate, int periodCount)
        {
            if (periodCount <= 0) return false;
            var startMonth = MonthStart(startDate);
            var endMonth = startMonth.AddMonths(periodCount - 1);
            var dueMonth = MonthStart(dueDate);
            return MonthDiff(dueMonth, endMonth) == 1;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> List(DateTime? from, DateTime? to, int? incomeTypeId)
        {
            var now = DateTime.Today;
            var defaultFrom = new DateTime(now.Year, now.Month, 1);
            var defaultToEx = defaultFrom.AddMonths(1);

            DateTime f, toEx;
            if (from == null || to == null)
            {
                f = defaultFrom;
                toEx = defaultToEx;
            }
            else
            {
                f = from.Value.Date;
                var t = to.Value.Date;
                if (t < f) (f, t) = (t, f);
                toEx = t.AddDays(1);
            }

            static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
            static int MonthDiff(DateTime aMonth, DateTime bMonth)
                => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

            var q = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Include(x => x.RecurringIncome)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                q = q.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            var raw = await q
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    amount = x.Amount,
                    dueDateDt = x.DueDate,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    incomeType = x.IncomeType.Name,
                    incomeTypeId = x.IncomeTypeId,
                    isReceived = x.IsReceived,

                    recurringIncomeId = x.RecurringIncomeId,
                    recurringStartDate = x.RecurringIncome != null ? (DateTime?)x.RecurringIncome.StartDate : null,
                    recurringPeriodCount = x.RecurringIncome != null ? x.RecurringIncome.PeriodCount : (int?)null
                })
                .ToListAsync();

            var list = raw.Select(x =>
            {
                string? periodText = null;

                if (x.recurringIncomeId.HasValue &&
                    x.recurringStartDate.HasValue &&
                    x.recurringPeriodCount.HasValue &&
                    x.recurringPeriodCount.Value > 0)
                {
                    var startM = MonthStart(x.recurringStartDate.Value);
                    var dueM = MonthStart(x.dueDateDt);
                    var idx = MonthDiff(startM, dueM) + 1;

                    if (idx < 1) idx = 1;
                    if (idx > x.recurringPeriodCount.Value) idx = x.recurringPeriodCount.Value;

                    periodText = $"{idx}/{x.recurringPeriodCount.Value}";
                }

                return new
                {
                    x.id,
                    x.name,
                    x.amount,
                    x.dueDate,
                    x.incomeType,
                    x.incomeTypeId,
                    x.isReceived,
                    recurringPeriodText = periodText
                };
            }).ToList();

            return Json(new
            {
                ok = true,
                unpaid = list.Where(x => !x.isReceived).ToList(),
                paid = list.Where(x => x.isReceived).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetReceived([FromBody] SetReceivedVm vm)
        {
            var ent = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null) return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            ent.IsReceived = vm.IsReceived;
            ent.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }

        public class SetReceivedVm
        {
            public int Id { get; set; }
            public bool IsReceived { get; set; }
        }
    }
}