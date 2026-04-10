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

            if (t < f)
                (f, t) = (t, f);

            return (f, t.AddDays(1));
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> List(DateTime? from, DateTime? to, int? debtTypeId, bool? isPaid)
        {
            await _recurring.EnsureGeneratedAsync(DateTime.Today);

            var (f, toEx) = Range(from, to);

            static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
            static int MonthDiff(DateTime aMonth, DateTime bMonth)
                => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

            var q = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Include(x => x.RecurringDebt)
                .Where(x => x.DueDate >= f && x.DueDate < toEx && x.IsDeleted==false);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                q = q.Where(x => x.DebtTypeId == debtTypeId.Value);

            if (isPaid.HasValue)
                q = q.Where(x => x.IsPaid == isPaid.Value);

            var raw = await q.OrderBy(x => x.DueDate).ThenBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    debtTypeId = x.DebtTypeId,
                    debtType = x.DebtType.Name,
                    name = x.Name,

                    netAmount = x.NetAmount,
                    taxAmount = x.TaxAmount,
                    amount = x.Amount,

                    dueDateDt = x.DueDate,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    payee = x.Payee,
                    isPaid = x.IsPaid,

                    recurringDebtId = x.RecurringDebtId,
                    recurringStartDate = x.RecurringDebt != null ? (DateTime?)x.RecurringDebt.StartDate : null,
                    recurringPeriodCount = x.RecurringDebt != null ? x.RecurringDebt.PeriodCount : (int?)null
                })
                .ToListAsync();

            var list = raw.Select(x =>
            {
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
                    x.debtTypeId,
                    x.debtType,
                    x.name,

                    x.netAmount,
                    x.taxAmount,
                    x.amount,

                    x.dueDate,
                    x.payee,
                    x.isPaid,

                    x.recurringDebtId,
                    x.recurringPeriodCount,
                    recurringPeriodText = periodText
                };
            }).ToList();

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

            var name = vm.Name.Trim();
            var payee = string.IsNullOrWhiteSpace(vm.Payee) ? null : vm.Payee.Trim();

            decimal net = vm.NetAmount;
            decimal tax = vm.TaxAmount;

            if (net < 0) net = 0;
            if (tax < 0) tax = 0;

            if (net + tax <= 0 && vm.Amount > 0)
            {
                net = vm.Amount;
                tax = 0;
            }

            var total = net + tax;
            if (total <= 0) return BadRequest(new { ok = false, message = "Net + Vergi toplamı 0'dan büyük olmalı." });

            int? periodCount = (vm.PeriodCount.HasValue && vm.PeriodCount.Value > 0)
                ? vm.PeriodCount.Value
                : (int?)null;

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
                            NetAmount = net,
                            TaxAmount = tax,
                            Amount = total,
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
                        if (vm.DueDate.Date < rule.StartDate.Date)
                            rule.StartDate = vm.DueDate.Date;

                        rule.NetAmount = net;
                        rule.TaxAmount = tax;
                        rule.Amount = total;
                        rule.Payee = payee;
                        rule.DayOfMonth = day;
                        rule.PeriodCount = periodCount;
                        rule.IsActive = true;
                        rule.UpdatedDate = DateTime.Now;

                        await _db.SaveChangesAsync();
                    }

                    recurringId = rule.Id;
                }

                var firstPaid = vm.IsPaid ? total : 0m;

                var ent = new Debt
                {
                    DebtTypeId = vm.DebtTypeId,
                    Name = name,
                    NetAmount = net,
                    TaxAmount = tax,
                    Amount = total,
                    PaidAmount = firstPaid,
                    CarryForwardBalance = total - firstPaid,
                    DueDate = vm.DueDate.Date,
                    Payee = payee,
                    IsPaid = vm.IsPaid,
                    UpdatedDate = DateTime.Now,
                    RecurringDebtId = recurringId
                };

                _db.Debts.Add(ent);
                await _db.SaveChangesAsync();

                if (vm.IsRecurring)
                    await _recurring.EnsureGeneratedAsync(DateTime.Today);

                return Json(new { ok = true, id = ent.Id });
            }

            var existing = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id && !x.IsDeleted);
            if (existing == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            var oldWasPaid = existing.IsPaid;
            var oldAmount = existing.Amount;

            var currentPaid = existing.PaidAmount ?? 0m;
            if (currentPaid < 0m) currentPaid = 0m;

            existing.DebtTypeId = vm.DebtTypeId;
            existing.Name = name;
            existing.NetAmount = net;
            existing.TaxAmount = tax;
            existing.Amount = total;
            existing.DueDate = vm.DueDate.Date;
            existing.Payee = payee;
            existing.UpdatedDate = DateTime.Now;

            if (vm.IsPaid)
            {
                if (currentPaid < total)
                    currentPaid = total;
            }
            else
            {
                // sadece checkbox ile tam ödendi yapılmış kayıt geri açılıyorsa sıfırla
                // fazla ödeme varsa koru
                if (oldWasPaid && currentPaid == oldAmount)
                    currentPaid = 0m;
            }

            existing.PaidAmount = currentPaid;
            existing.IsPaid = currentPaid >= total;
            existing.CarryForwardBalance = total - currentPaid;

            if (!vm.IsRecurring)
            {
                existing.RecurringDebtId = null;
                await _db.SaveChangesAsync();
                return Json(new { ok = true });
            }

            {
                var day = vm.DueDate.Day;
                RecurringDebt? rule = null;

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
                            NetAmount = net,
                            TaxAmount = tax,
                            Amount = total,
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
                rule.NetAmount = net;
                rule.TaxAmount = tax;
                rule.Amount = total;
                rule.Payee = payee;
                rule.DayOfMonth = day;

                if (vm.DueDate.Date < rule.StartDate.Date)
                    rule.StartDate = vm.DueDate.Date;

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
            // YENİ: Zaten silinmiş bir kaydı getirmemesi için && !x.IsDeleted eklendi
            var ent = await _db.Debts.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı veya zaten silinmiş." });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (ent.RecurringDebtId.HasValue)
                {
                    var month = new DateTime(ent.DueDate.Year, ent.DueDate.Month, 1);
                    var rid = ent.RecurringDebtId.Value;

                    var already = await _db.RecurringDebtSkips
                        .AnyAsync(s => s.RecurringDebtId == rid && s.Month == month);

                    if (!already)
                    {
                        _db.RecurringDebtSkips.Add(new RecurringDebtSkip
                        {
                            RecurringDebtId = rid,
                            Month = month,
                            CreateDate = DateTime.Now
                        });
                    }
                }

                // ESKİ HALİ: _db.Debts.Remove(ent);
                // YENİ HALİ: Soft Delete yapıyoruz
                ent.IsDeleted = true;
                ent.UpdatedDate = DateTime.Now; // Silinme tarihini/son güncellemeyi tutmak iyi bir pratiktir

                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return Json(new { ok = true });
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                return BadRequest(new { ok = false, message = "Silinemedi (veritabanı işlemi sırasında hata oluştu)." });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { ok = false, message = "Silinemedi: " + ex.Message });
            }
        }

        public async Task<IActionResult> Detail(int id, DateTime? fromDate, DateTime? toDate)
        {
            var now = DateTime.Now;

            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var startDate = (fromDate ?? firstDayOfMonth).Date;
            var endDate = (toDate ?? lastDayOfMonth).Date;

            if (endDate < startDate)
            {
                var temp = startDate;
                startDate = endDate;
                endDate = temp;
            }

            var debtType = await _db.DebtTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (debtType == null)
                return NotFound();

            var debts = await _db.Debts
                .AsNoTracking()
                .Where(x => x.DebtTypeId == id
                            && x.DueDate >= startDate
                            && x.DueDate <= endDate && !x.IsDeleted)
                .OrderBy(x => x.DueDate)
                .ToListAsync();

            ViewBag.DebtTypeId = debtType.Id;
            ViewBag.DebtTypeName = debtType.Name;
            ViewBag.FromDate = startDate;
            ViewBag.ToDate = endDate;

            return View(debts);
        }
    }
}