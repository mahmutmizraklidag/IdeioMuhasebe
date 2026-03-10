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
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

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

                    dueDateDt = x.DueDate, // hesap için
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

                    var idx = MonthDiff(startM, dueM) + 1; // 1-based
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

                    // ✅ frontend bunu gösterecek
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

            // ✅ Net+Vergi -> Toplam (geriye dönük uyum: net+tax 0 ise Amount kullan)
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

            int? periodCount = (vm.PeriodCount.HasValue && vm.PeriodCount.Value > 0) ? vm.PeriodCount.Value : (int?)null;

            // -------------------------
            // CREATE
            // -------------------------
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
                        await _db.SaveChangesAsync(); // rule.Id
                    }
                    else
                    {
                        if (vm.DueDate.Date < rule.StartDate.Date) rule.StartDate = vm.DueDate.Date;

                        rule.NetAmount = net;
                        rule.TaxAmount = tax;
                        rule.Amount = net + tax;

                        rule.Payee = payee;
                        rule.DayOfMonth = day;
                        rule.PeriodCount = periodCount;
                        rule.IsActive = true;
                        rule.UpdatedDate = DateTime.Now;

                        await _db.SaveChangesAsync();
                    }

                    recurringId = rule.Id;
                }

                var ent = new Debt
                {
                    DebtTypeId = vm.DebtTypeId,
                    Name = name,

                    NetAmount = net,
                    TaxAmount = tax,
                    Amount = total,

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

            // -------------------------
            // UPDATE
            // -------------------------
            var existing = await _db.Debts.FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (existing == null) return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            existing.DebtTypeId = vm.DebtTypeId;
            existing.Name = name;

            existing.NetAmount = net;
            existing.TaxAmount = tax;
            existing.Amount = total;

            existing.DueDate = vm.DueDate.Date;
            existing.Payee = payee;
            existing.IsPaid = vm.IsPaid;
            existing.UpdatedDate = DateTime.Now;

            // ✅ Yinelenen kapandıysa sadece bu kaydı kuraldan kopar
            if (!vm.IsRecurring)
            {
                existing.RecurringDebtId = null;
                await _db.SaveChangesAsync();
                return Json(new { ok = true });
            }

            // ✅ Yinelenen açık: rule oluştur/güncelle ve bağla
            {
                var day = vm.DueDate.Day;

                RecurringDebt rule = null;

                // varsa bağlı rule'u çek
                if (existing.RecurringDebtId.HasValue)
                    rule = await _db.RecurringDebts.FirstOrDefaultAsync(r => r.Id == existing.RecurringDebtId.Value);

                // yoksa eşleşeni bul / yoksa oluştur
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

                // rule güncelle
                rule.DebtTypeId = vm.DebtTypeId;
                rule.Name = name;

                rule.NetAmount = net;
                rule.TaxAmount = tax;
                rule.Amount = net + tax;

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
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // ✅ Yinelenen kayıtsa: bu ayı skip'le ki tekrar üretilmesin
                if (ent.RecurringDebtId.HasValue)
                {
                    var month = new DateTime(ent.DueDate.Year, ent.DueDate.Month, 1);
                    var rid = ent.RecurringDebtId.Value;

                    var already = await _db.RecurringDebtSkips
                        .AnyAsync(s => s.RecurringDebtId == rid && s.Month == month);

                    if (!already)
                    {
                        _db.RecurringDebtSkips.Add(new IdeioMuhasebe.Entities.RecurringDebtSkip
                        {
                            RecurringDebtId = rid,
                            Month = month,
                            CreateDate = DateTime.Now
                        });
                    }
                }

                _db.Debts.Remove(ent);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return Json(new { ok = true });
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                return BadRequest(new { ok = false, message = "Silinemedi (veritabanı kısıtı/ilişki hatası)." });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { ok = false, message = "Silinemedi: " + ex.Message });
            }
        }
    }
}