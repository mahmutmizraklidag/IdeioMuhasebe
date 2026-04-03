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

            var raw = await q
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    totalAmount = x.Amount,
                    receivedAmount = x.ReceivedAmount,
                    carryForwardBalance = x.CarryForwardBalance,
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
                var received = x.receivedAmount;
                if (received < 0m) received = 0m;

                var remaining = x.totalAmount - received;
                if (remaining < 0m) remaining = 0m;

                var carryCredit = x.carryForwardBalance < 0m ? Math.Abs(x.carryForwardBalance) : 0m;
                var resolvedIsReceived = received >= x.totalAmount || x.totalAmount == 0m;

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
                    amount = remaining,
                    totalAmount = x.totalAmount,
                    receivedAmount = received,
                    remainingAmount = remaining,
                    carryForwardCredit = carryCredit,
                    x.dueDate,
                    x.incomeType,
                    x.incomeTypeId,
                    isReceived = resolvedIsReceived,
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
        public async Task<IActionResult> SetReceived([FromBody] SetReceivedVm vm)
        {
            if (vm == null || vm.Id <= 0)
                return BadRequest(new { ok = false, message = "Geçersiz istek." });

            var ent = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            var currentReceived = ent.ReceivedAmount;
            if (currentReceived < 0m) currentReceived = 0m;

            if (vm.IsReceived) // "Tahsil Edilenler" sütununa taşındıysa
            {
                // Eğer mevcut tahsilat borç miktarından az ise, tam miktar yap
                if (currentReceived < ent.Amount)
                    ent.ReceivedAmount = ent.Amount;
            }
            else // "Bekleyenler" sütununa taşındıysa
            {
                // Önemli Değişiklik: Eğer tam ödenmişse VEYA fazla ödenmişse sıfırla
                if (currentReceived >= ent.Amount)
                    ent.ReceivedAmount = 0m;
            }

            // Durum ve bakiye güncellemeleri
            ent.IsReceived = ent.ReceivedAmount >= ent.Amount;
            ent.CarryForwardBalance = ent.Amount - ent.ReceivedAmount;
            ent.UpdatedDate = DateTime.Now;

            await _db.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                receivedAmount = ent.ReceivedAmount,
                remainingAmount = Math.Max(0m, ent.Amount - ent.ReceivedAmount),
                carryForwardCredit = ent.CarryForwardBalance < 0m ? Math.Abs(ent.CarryForwardBalance) : 0m,
                fullyCovered = ent.IsReceived
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPartialReceive([FromBody] PartialAmountVM vm)
        {
            if (vm == null || vm.Id <= 0)
                return BadRequest(new { ok = false, message = "Geçersiz istek." });

            if (vm.Amount <= 0)
                return BadRequest(new { ok = false, message = "Tutar 0'dan büyük olmalı." });

            var ent = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            var currentReceived = ent.ReceivedAmount;
            if (currentReceived < 0m) currentReceived = 0m;

            ent.ReceivedAmount = currentReceived + vm.Amount;
            ent.IsReceived = ent.ReceivedAmount >= ent.Amount;
            ent.CarryForwardBalance = ent.Amount - ent.ReceivedAmount;
            ent.UpdatedDate = DateTime.Now;

            await _db.SaveChangesAsync();

            var received = ent.ReceivedAmount;
            var remaining = ent.Amount - received;
            if (remaining < 0m) remaining = 0m;

            return Json(new
            {
                ok = true,
                receivedAmount = received,
                remainingAmount = remaining,
                carryForwardCredit = ent.CarryForwardBalance < 0m ? Math.Abs(ent.CarryForwardBalance) : 0m,
                fullyCovered = ent.IsReceived
            });
        }
    }
}