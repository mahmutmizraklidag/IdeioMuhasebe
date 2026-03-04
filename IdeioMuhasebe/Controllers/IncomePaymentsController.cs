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
            await _recurring.EnsureGeneratedAsync(DateTime.Today);

            var (f, toEx) = Range(from, to);

            var q = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Include(x => x.RecurringIncome)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                q = q.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            var baseList = await q
                .OrderBy(x => x.DueDate)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    amount = x.Amount,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    incomeType = x.IncomeType.Name,
                    incomeTypeId = x.IncomeTypeId,
                    isReceived = x.IsReceived,
                    lastPeriodWarning = (x.RecurringIncome != null
                        && x.RecurringIncome.PeriodCount.HasValue
                        && x.RecurringIncome.PeriodCount.Value > 0
                        && LastOnePeriodWarning(x.DueDate, x.RecurringIncome.StartDate, x.RecurringIncome.PeriodCount.Value))
                })
                .ToListAsync();

            var unpaid = baseList.Where(x => !x.isReceived).ToList();
            var paid = baseList.Where(x => x.isReceived).ToList();

            return Json(new { ok = true, unpaid, paid });
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