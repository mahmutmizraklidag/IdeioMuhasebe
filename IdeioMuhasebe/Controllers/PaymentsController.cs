using System;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Models;
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
            if (from == null || to == null)
                return DefaultMonthRange();

            var f = from.Value.Date;
            var t = to.Value.Date;

            if (t < f)
                (f, t) = (t, f);

            return (f, t.AddDays(1));
        }

        private static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);

        private static int MonthDiff(DateTime aMonth, DateTime bMonth)
            => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> List(DateTime? from, DateTime? to, int? debtTypeId)
        {
            await _recurring.EnsureGeneratedAsync(DateTime.Today);

            var (f, toEx) = Range(from, to);

            var q = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Include(x => x.RecurringDebt)
                .Where(x => x.DueDate >= f && x.DueDate < toEx && x.IsDeleted == false);

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
                    carryForwardBalance = x.CarryForwardBalance,
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
                var paid = x.paidAmount ?? 0m;
                if (paid < 0m) paid = 0m;

                var remaining = x.totalAmount - paid;
                if (remaining < 0m) remaining = 0m;

                var carryCredit = x.carryForwardBalance < 0m ? Math.Abs(x.carryForwardBalance) : 0m;
                var resolvedIsPaid = paid >= x.totalAmount || x.totalAmount == 0m;

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
                    carryForwardCredit = carryCredit,
                    x.dueDate,
                    x.debtType,
                    x.debtTypeId,
                    isPaid = resolvedIsPaid,
                    recurringPeriodText = periodText
                };
            }).ToList();

            return Json(new
            {
                ok = true,
                unpaid = list.Where(x => x.remainingAmount > 0m).ToList(),
                paid = list.Where(x => x.remainingAmount <= 0m).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPaid([FromBody] SetPaidVm vm)
        {
            if (vm == null || vm.Id <= 0)
                return BadRequest(new { ok = false, message = "Geçersiz istek." });

            var ent = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            var currentPaid = ent.PaidAmount ?? 0m;

            if (vm.IsPaid)
            {
                // Ödendiye çekildiğinde: Eğer borçtan az ödenmişse, borç miktarına tamamla
                if (currentPaid < ent.Amount)
                    ent.PaidAmount = ent.Amount;
            }
            else
            {
                // Ödenmediye çekildiğinde: 
                // Eğer ödenen miktar borca eşit VEYA borçtan fazlaysa sıfırla (veya borcun altına çek)
                if (currentPaid >= ent.Amount)
                    ent.PaidAmount = 0m;

                // Not: Eğer fazla ödemeyi korumak ama statüyü "Ödenmedi" yapmak istiyorsanız 
                // buradaki mantığı iş kuralınıza göre değiştirebilirsiniz. 
                // Ancak teknik olarak IsPaid'in false olması için PaidAmount < Amount olmalıdır.
            }

            ent.IsPaid = (ent.PaidAmount ?? 0m) >= ent.Amount;
            ent.CarryForwardBalance = ent.Amount - (ent.PaidAmount ?? 0m);
            ent.UpdatedDate = DateTime.Now;

            await _db.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                paidAmount = ent.PaidAmount,
                remainingAmount = Math.Max(0m, ent.Amount - (ent.PaidAmount ?? 0m)),
                carryForwardCredit = ent.CarryForwardBalance < 0m ? Math.Abs(ent.CarryForwardBalance) : 0m,
                fullyCovered = ent.IsPaid
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPartialPayment([FromBody] PartialAmountVM vm)
        {
            if (vm == null || vm.Id <= 0)
                return BadRequest(new { ok = false, message = "Geçersiz istek." });

            if (vm.Amount <= 0)
                return BadRequest(new { ok = false, message = "Tutar 0'dan büyük olmalı." });

            var ent = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            var currentPaid = ent.PaidAmount ?? 0m;
            if (currentPaid < 0m) currentPaid = 0m;

            ent.PaidAmount = currentPaid + vm.Amount;
            ent.IsPaid = ent.PaidAmount >= ent.Amount;
            ent.CarryForwardBalance = ent.Amount - (ent.PaidAmount ?? 0m);
            ent.UpdatedDate = DateTime.Now;

            await _db.SaveChangesAsync();

            var paid = ent.PaidAmount ?? 0m;
            var remaining = ent.Amount - paid;
            if (remaining < 0m) remaining = 0m;

            return Json(new
            {
                ok = true,
                paidAmount = paid,
                remainingAmount = remaining,
                carryForwardCredit = ent.CarryForwardBalance < 0m ? Math.Abs(ent.CarryForwardBalance) : 0m,
                fullyCovered = ent.IsPaid
            });
        }
    }
}