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
    public class PaymentsController : Controller
    {
        private readonly DatabaseContext _db;
        private readonly RecurringDebtService _recurring;

        public PaymentsController(DatabaseContext db, RecurringDebtService recurring)
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
        public async Task<IActionResult> List(DateTime? from, DateTime? to, int? debtTypeId)
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

            static decimal Clamp(decimal value, decimal max)
            {
                if (value < 0) return 0m;
                if (value > max) return max;
                return value;
            }

            var q = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Include(x => x.RecurringDebt)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                q = q.Where(x => x.DebtTypeId == debtTypeId.Value);

            var raw = await q
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    totalAmount = x.Amount,
                    paidAmount = x.PaidAmount,
                    dueDateDt = x.DueDate,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    debtType = x.DebtType.Name,
                    debtTypeId = x.DebtTypeId,
                    isPaid = x.IsPaid,

                    recurringDebtId = x.RecurringDebtId,
                    recurringStartDate = x.RecurringDebt != null ? (DateTime?)x.RecurringDebt.StartDate : null,
                    recurringPeriodCount = x.RecurringDebt != null ? x.RecurringDebt.PeriodCount : (int?)null
                })
                .ToListAsync();

            var list = raw.Select(x =>
            {
                var paid = Clamp(x.paidAmount ?? 0, x.totalAmount);
                var remaining = x.totalAmount - paid;

                string? periodText = null;

                if (x.recurringDebtId.HasValue &&
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
                    amount = remaining,
                    totalAmount = x.totalAmount,
                    paidAmount = paid,
                    remainingAmount = remaining,
                    x.dueDate,
                    x.debtType,
                    x.debtTypeId,
                    x.isPaid,
                    recurringPeriodText = periodText
                };
            }).ToList();

            return Json(new
            {
                ok = true,
                unpaid = list.Where(x => !x.isPaid).ToList(),
                paid = list.Where(x => x.isPaid).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPaid([FromBody] IdeioMuhasebe.Models.SetPaidVm vm)
        {
            if (vm == null || vm.Id <= 0)
                return BadRequest(new { ok = false, message = "Geçersiz istek." });

            var ent = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            if (vm.IsPaid)
            {
                ent.IsPaid = true;
                ent.PaidAmount = ent.Amount;
            }
            else
            {
                ent.IsPaid = false;

                // sadece fully-paid kolondan geri çekildiyse sıfırla
                if (ent.PaidAmount >= ent.Amount)
                    ent.PaidAmount = 0m;
            }

            ent.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPartialPayment([FromBody] IdeioMuhasebe.Models.PartialAmountVM vm)
        {
            if (vm == null || vm.Id <= 0)
                return BadRequest(new { ok = false, message = "Geçersiz istek." });

            if (vm.Amount <= 0)
                return BadRequest(new { ok = false, message = "Tutar 0'dan büyük olmalı." });

            var ent = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            if (ent.IsPaid)
                return BadRequest(new { ok = false, message = "Bu kayıt zaten ödendi." });

            var currentPaid = ent.PaidAmount < 0 ? 0m : ent.PaidAmount;
            if (currentPaid > ent.Amount) currentPaid = ent.Amount;

            var remaining = ent.Amount - currentPaid;

            // ✅ artık clamp yok, fazla girilirse reddet
            if (vm.Amount > remaining)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = $"Gider tutarından daha büyük bir ödeme girdiniz. Kalan tutar: {remaining:N2} ₺"
                });
            }

            ent.PaidAmount = currentPaid + vm.Amount;

            // ✅ toplam tamamlandıysa otomatik ödendi yap
            if (ent.PaidAmount >= ent.Amount)
            {
                ent.PaidAmount = ent.Amount;
                ent.IsPaid = true;
            }
            else
            {
                ent.IsPaid = false;
            }

            ent.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                paidAmount = ent.PaidAmount,
                remainingAmount = ent.Amount - ent.PaidAmount,
                fullyCovered = ent.IsPaid
            });
        }

        public class SetPaidVm
        {
            public int Id { get; set; }
            public bool IsPaid { get; set; }
        }
    }
}