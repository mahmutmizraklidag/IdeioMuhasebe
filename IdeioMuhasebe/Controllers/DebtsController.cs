using System;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Entities;
using IdeioMuhasebe.Models;
using IdeioMuhasebe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class DebtsController : Controller
    {
        private readonly DatabaseContext _db;
        private readonly RecurringDebtService _recurring;

        public DebtsController(DatabaseContext db, RecurringDebtService recurring)
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
            var left = MonthDiff(dueMonth, endMonth);
            return left == 1;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> List(DateTime? from, DateTime? to, int? debtTypeId, bool? isPaid)
        {
            await _recurring.EnsureGeneratedAsync(DateTime.Today);

            var (f, toEx) = Range(from, to);

            var q = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Include(x => x.RecurringDebt)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                q = q.Where(x => x.DebtTypeId == debtTypeId.Value);

            if (isPaid.HasValue)
                q = q.Where(x => x.IsPaid == isPaid.Value);

            var list = await q
                .OrderBy(x => x.DueDate).ThenBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    debtTypeId = x.DebtTypeId,
                    debtType = x.DebtType.Name,
                    name = x.Name,
                    amount = x.Amount,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    payee = x.Payee,
                    isPaid = x.IsPaid,

                    recurringDebtId = x.RecurringDebtId,
                    recurringPeriodCount = x.RecurringDebt != null ? x.RecurringDebt.PeriodCount : null,
                    lastPeriodWarning = (x.RecurringDebt != null
                        && x.RecurringDebt.PeriodCount.HasValue
                        && x.RecurringDebt.PeriodCount.Value > 0
                        && LastOnePeriodWarning(x.DueDate, x.RecurringDebt.StartDate, x.RecurringDebt.PeriodCount.Value))
                })
                .ToListAsync();

            return Json(new { ok = true, list });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert([FromBody] DebtUpsertVm vm)
        {
            if (vm == null) return BadRequest(new { ok = false, message = "Geçersiz istek." });
            if (vm.DebtTypeId <= 0) return BadRequest(new { ok = false, message = "Kategori seçmelisiniz." });
            if (string.IsNullOrWhiteSpace(vm.Name)) return BadRequest(new { ok = false, message = "Borç adı zorunludur." });
            if (vm.DueDate == default) return BadRequest(new { ok = false, message = "Tarih zorunludur." });
            if (vm.Amount <= 0) return BadRequest(new { ok = false, message = "Tutar 0'dan büyük olmalı." });

            var name = vm.Name.Trim();
            var payee = string.IsNullOrWhiteSpace(vm.Payee) ? null : vm.Payee.Trim();

            int? periodCount = (vm.PeriodCount.HasValue && vm.PeriodCount.Value > 0) ? vm.PeriodCount.Value : (int?)null;

            if (vm.Id == 0)
            {
                int? recurringId = null;

                if (vm.IsRecurring)
                {
                    var day = vm.DueDate.Day;

                    var rule = await _db.RecurringDebts.FirstOrDefaultAsync(r =>
                        r.IsActive &&
                        r.DebtTypeId == vm.DebtTypeId &&
                        r.Name == name &&
                        (r.Payee ?? "") == (payee ?? "") &&
                        r.DayOfMonth == day
                    );

                    if (rule == null)
                    {
                        rule = new RecurringDebt
                        {
                            DebtTypeId = vm.DebtTypeId,
                            Name = name,
                            Amount = vm.Amount,
                            Payee = payee,
                            DayOfMonth = day,
                            StartDate = vm.DueDate.Date,
                            PeriodCount = periodCount,
                            IsActive = true,
                            UpdatedDate = DateTime.Now
                        };
                        _db.RecurringDebts.Add(rule);
                        await _db.SaveChangesAsync();
                    }
                    else
                    {
                        if (vm.DueDate.Date < rule.StartDate.Date) rule.StartDate = vm.DueDate.Date;
                        rule.Amount = vm.Amount;
                        rule.Payee = payee;
                        rule.PeriodCount = periodCount;
                        rule.UpdatedDate = DateTime.Now;
                        await _db.SaveChangesAsync();
                    }

                    recurringId = rule.Id;
                }

                var ent = new Debt
                {
                    DebtTypeId = vm.DebtTypeId,
                    Name = name,
                    Amount = vm.Amount,
                    DueDate = vm.DueDate.Date,
                    Payee = payee,
                    IsPaid = vm.IsPaid,
                    UpdatedDate = DateTime.Now,
                    RecurringDebtId = recurringId
                };

                _db.Debts.Add(ent);
                await _db.SaveChangesAsync();

                if (recurringId.HasValue)
                    await _recurring.EnsureGeneratedAsync(DateTime.Today);

                return Json(new { ok = true, id = ent.Id });
            }

            var existing = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (existing == null) return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            existing.DebtTypeId = vm.DebtTypeId;
            existing.Name = name;
            existing.Amount = vm.Amount;
            existing.DueDate = vm.DueDate.Date;
            existing.Payee = payee;
            existing.IsPaid = vm.IsPaid;
            existing.UpdatedDate = DateTime.Now;

            if (!vm.IsRecurring)
            {
                existing.RecurringDebtId = null;
                await _db.SaveChangesAsync();
                return Json(new { ok = true });
            }

            // recurring açık: rule oluştur/güncelle
            {
                var day = vm.DueDate.Day;

                RecurringDebt rule = null;
                if (existing.RecurringDebtId.HasValue)
                    rule = await _db.RecurringDebts.FirstOrDefaultAsync(r => r.Id == existing.RecurringDebtId.Value);

                if (rule == null)
                {
                    rule = await _db.RecurringDebts.FirstOrDefaultAsync(r =>
                        r.IsActive &&
                        r.DebtTypeId == vm.DebtTypeId &&
                        r.Name == name &&
                        (r.Payee ?? "") == (payee ?? "") &&
                        r.DayOfMonth == day
                    );

                    if (rule == null)
                    {
                        rule = new RecurringDebt
                        {
                            DebtTypeId = vm.DebtTypeId,
                            Name = name,
                            Amount = vm.Amount,
                            Payee = payee,
                            DayOfMonth = day,
                            StartDate = vm.DueDate.Date,
                            PeriodCount = periodCount,
                            IsActive = true,
                            UpdatedDate = DateTime.Now
                        };
                        _db.RecurringDebts.Add(rule);
                        await _db.SaveChangesAsync();
                    }
                }

                rule.DebtTypeId = vm.DebtTypeId;
                rule.Name = name;
                rule.Amount = vm.Amount;
                rule.Payee = payee;
                rule.DayOfMonth = day;
                if (vm.DueDate.Date < rule.StartDate.Date) rule.StartDate = vm.DueDate.Date;
                rule.PeriodCount = periodCount;
                rule.IsActive = true;
                rule.UpdatedDate = DateTime.Now;

                existing.RecurringDebtId = rule.Id;

                await _db.SaveChangesAsync();
                await _recurring.EnsureGeneratedAsync(DateTime.Today);
            }

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            var ent = await _db.Debts.FirstOrDefaultAsync(x => x.Id == id);
            if (ent == null) return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            // ✅ Yinelenen kayıtsa: o ayı “skip” olarak işaretle ki tekrar üretilmesin
            if (ent.RecurringDebtId.HasValue)
            {
                var m = new DateTime(ent.DueDate.Year, ent.DueDate.Month, 1);
                var rid = ent.RecurringDebtId.Value;

                var exists = await _db.RecurringDebtSkips.AnyAsync(s => s.RecurringDebtId == rid && s.Month == m);
                if (!exists)
                {
                    _db.RecurringDebtSkips.Add(new Entities.RecurringDebtSkip
                    {
                        RecurringDebtId = rid,
                        Month = m
                    });
                }
            }

            _db.Debts.Remove(ent);
            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }
    }
}