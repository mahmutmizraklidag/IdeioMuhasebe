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
    public class IncomesController : Controller
    {
        private readonly DatabaseContext _db;
        private readonly RecurringIncomeService _recurring;

        public IncomesController(DatabaseContext db, RecurringIncomeService recurring)
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
        public async Task<IActionResult> List(DateTime? from, DateTime? to, int? incomeTypeId, bool? isReceived)
        {
            await _recurring.EnsureGeneratedAsync(DateTime.Today);

            var (f, toEx) = Range(from, to);

            static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
            static int MonthDiff(DateTime aMonth, DateTime bMonth)
                => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

            var q = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Include(x => x.RecurringIncome)
                .Where(x => x.DueDate >= f && x.DueDate < toEx && x.IsDeleted == false);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                q = q.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            if (isReceived.HasValue)
                q = q.Where(x => x.IsReceived == isReceived.Value);

            var raw = await q.OrderBy(x => x.DueDate).ThenBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    incomeTypeId = x.IncomeTypeId,
                    incomeType = x.IncomeType.Name,
                    name = x.Name,

                    netAmount = x.NetAmount,
                    taxAmount = x.TaxAmount,
                    amount = x.Amount,

                    dueDateDt = x.DueDate,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    payer = x.Payer,
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
                    x.incomeTypeId,
                    x.incomeType,
                    x.name,

                    x.netAmount,
                    x.taxAmount,
                    x.amount,

                    x.dueDate,
                    x.payer,
                    x.isReceived,

                    x.recurringIncomeId,
                    x.recurringPeriodCount,
                    recurringPeriodText = periodText
                };
            }).ToList();

            return Json(new { ok = true, list });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert([FromBody] IncomeUpsertVm vm)
        {
            if (vm == null) return BadRequest(new { ok = false, message = "Geçersiz istek." });
            if (vm.IncomeTypeId <= 0) return BadRequest(new { ok = false, message = "Kategori seçmelisiniz." });
            if (string.IsNullOrWhiteSpace(vm.Name)) return BadRequest(new { ok = false, message = "Gelir adı zorunludur." });
            if (vm.DueDate == default) return BadRequest(new { ok = false, message = "Tarih zorunludur." });

            var name = vm.Name.Trim();
            var payer = string.IsNullOrWhiteSpace(vm.Payer) ? null : vm.Payer.Trim();

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
            if (total <= 0)
                return BadRequest(new { ok = false, message = "Net + Vergi toplamı 0'dan büyük olmalı." });

            int? periodCount = (vm.PeriodCount.HasValue && vm.PeriodCount.Value > 0)
                ? vm.PeriodCount.Value
                : (int?)null;

            if (vm.Id == 0)
            {
                int? recurringId = null;

                if (vm.IsRecurring)
                {
                    var day = vm.DueDate.Day;

                    var rule = await _db.RecurringIncomes.FirstOrDefaultAsync(r =>
                        r.IsActive &&
                        r.IncomeTypeId == vm.IncomeTypeId &&
                        r.Name == name &&
                        (r.Payer ?? "") == (payer ?? "") &&
                        r.DayOfMonth == day
                    );

                    if (rule == null)
                    {
                        rule = new RecurringIncome
                        {
                            IncomeTypeId = vm.IncomeTypeId,
                            Name = name,
                            NetAmount = net,
                            TaxAmount = tax,
                            Amount = total,
                            Payer = payer,
                            DayOfMonth = day,
                            StartDate = vm.DueDate.Date,
                            PeriodCount = periodCount,
                            IsActive = true,
                            UpdatedDate = DateTime.Now
                        };

                        _db.RecurringIncomes.Add(rule);
                        await _db.SaveChangesAsync();
                    }
                    else
                    {
                        if (vm.DueDate.Date < rule.StartDate.Date)
                            rule.StartDate = vm.DueDate.Date;

                        rule.NetAmount = net;
                        rule.TaxAmount = tax;
                        rule.Amount = total;
                        rule.Payer = payer;
                        rule.DayOfMonth = day;
                        rule.PeriodCount = periodCount;
                        rule.IsActive = true;
                        rule.UpdatedDate = DateTime.Now;

                        await _db.SaveChangesAsync();
                    }

                    recurringId = rule.Id;
                }

                var firstReceived = vm.IsReceived ? total : 0m;

                var ent = new Income
                {
                    IncomeTypeId = vm.IncomeTypeId,
                    Name = name,
                    NetAmount = net,
                    TaxAmount = tax,
                    Amount = total,
                    ReceivedAmount = firstReceived,
                    CarryForwardBalance = total - firstReceived,
                    DueDate = vm.DueDate.Date,
                    Payer = payer,
                    IsReceived = vm.IsReceived,
                    UpdatedDate = DateTime.Now,
                    RecurringIncomeId = recurringId
                };

                _db.Incomes.Add(ent);
                await _db.SaveChangesAsync();

                if (recurringId.HasValue)
                    await _recurring.EnsureGeneratedAsync(DateTime.Today);

                return Json(new { ok = true, id = ent.Id });
            }

            var existing = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == vm.Id && !x.IsDeleted);
            if (existing == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

            var oldWasReceived = existing.IsReceived;
            var oldAmount = existing.Amount;

            var currentReceived = existing.ReceivedAmount;
            if (currentReceived < 0m) currentReceived = 0m;

            existing.IncomeTypeId = vm.IncomeTypeId;
            existing.Name = name;
            existing.NetAmount = net;
            existing.TaxAmount = tax;
            existing.Amount = total;
            existing.DueDate = vm.DueDate.Date;
            existing.Payer = payer;
            existing.UpdatedDate = DateTime.Now;

            if (vm.IsReceived)
            {
                if (currentReceived < total)
                    currentReceived = total;
            }
            else
            {
                if (oldWasReceived && currentReceived == oldAmount)
                    currentReceived = 0m;
            }

            existing.ReceivedAmount = currentReceived;
            existing.IsReceived = currentReceived >= total;
            existing.CarryForwardBalance = total - currentReceived;

            if (!vm.IsRecurring)
            {
                existing.RecurringIncomeId = null;
                await _db.SaveChangesAsync();
                return Json(new { ok = true });
            }

            {
                var day = vm.DueDate.Day;
                RecurringIncome? rule = null;

                if (existing.RecurringIncomeId.HasValue)
                    rule = await _db.RecurringIncomes.FirstOrDefaultAsync(r => r.Id == existing.RecurringIncomeId.Value);

                if (rule == null)
                {
                    rule = await _db.RecurringIncomes.FirstOrDefaultAsync(r =>
                        r.IsActive &&
                        r.IncomeTypeId == vm.IncomeTypeId &&
                        r.Name == name &&
                        (r.Payer ?? "") == (payer ?? "") &&
                        r.DayOfMonth == day
                    );

                    if (rule == null)
                    {
                        rule = new RecurringIncome
                        {
                            IncomeTypeId = vm.IncomeTypeId,
                            Name = name,
                            NetAmount = net,
                            TaxAmount = tax,
                            Amount = total,
                            Payer = payer,
                            DayOfMonth = day,
                            StartDate = vm.DueDate.Date,
                            PeriodCount = periodCount,
                            IsActive = true,
                            UpdatedDate = DateTime.Now
                        };

                        _db.RecurringIncomes.Add(rule);
                        await _db.SaveChangesAsync();
                    }
                }

                rule.IncomeTypeId = vm.IncomeTypeId;
                rule.Name = name;
                rule.NetAmount = net;
                rule.TaxAmount = tax;
                rule.Amount = total;
                rule.Payer = payer;
                rule.DayOfMonth = day;

                if (vm.DueDate.Date < rule.StartDate.Date)
                    rule.StartDate = vm.DueDate.Date;

                rule.PeriodCount = periodCount;
                rule.IsActive = true;
                rule.UpdatedDate = DateTime.Now;

                existing.RecurringIncomeId = rule.Id;

                await _db.SaveChangesAsync();
                await _recurring.EnsureGeneratedAsync(DateTime.Today);
            }

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            // YENİ: && !x.IsDeleted eklendi
            var ent = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (ent == null)
                return NotFound(new { ok = false, message = "Kayıt bulunamadı veya zaten silinmiş." });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (ent.RecurringIncomeId.HasValue)
                {
                    var month = new DateTime(ent.DueDate.Year, ent.DueDate.Month, 1);
                    var rid = ent.RecurringIncomeId.Value;

                    var already = await _db.RecurringIncomeSkips
                        .AnyAsync(s => s.RecurringIncomeId == rid && s.Month == month);

                    if (!already)
                    {
                        _db.RecurringIncomeSkips.Add(new RecurringIncomeSkip
                        {
                            RecurringIncomeId = rid,
                            Month = month,
                            CreateDate = DateTime.Now
                        });
                    }
                }

                // ESKİ HALİ: _db.Incomes.Remove(ent);
                // YENİ HALİ: Soft Delete
                ent.IsDeleted = true;
                ent.UpdatedDate = DateTime.Now;

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