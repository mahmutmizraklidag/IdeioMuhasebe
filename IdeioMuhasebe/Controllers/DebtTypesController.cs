using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class DebtTypesController : Controller
    {
        private readonly DatabaseContext _db;

        public DebtTypesController(DatabaseContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index() => View();

        private static DateTime MonthStart(DateTime d) => new DateTime(d.Year, d.Month, 1);
        private static int MonthDiff(DateTime aMonth, DateTime bMonth)
            => (bMonth.Year - aMonth.Year) * 12 + (bMonth.Month - aMonth.Month);

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var types = await _db.DebtTypes.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            // Warning hesapla (in-memory, sade ve güvenli)
            var nowMonth = MonthStart(DateTime.Today);

            var rules = await _db.RecurringDebts.AsNoTracking()
                .Where(r => r.IsActive && r.PeriodCount.HasValue && r.PeriodCount.Value > 0)
                .Select(r => new { r.DebtTypeId, r.StartDate, r.PeriodCount })
                .ToListAsync();

            var warnSet = new HashSet<int>();
            foreach (var r in rules)
            {
                var startMonth = MonthStart(r.StartDate);
                var endMonth = startMonth.AddMonths(r.PeriodCount.Value - 1);

                var left = MonthDiff(nowMonth, endMonth);
                if (left == 1) warnSet.Add(r.DebtTypeId);
            }

            var list = types.Select(t => new
            {
                t.id,
                t.name,
                lastPeriodWarning = warnSet.Contains(t.id)
            }).ToList();

            return Json(new { ok = true, list });
        }

        [HttpGet]
        public async Task<IActionResult> Options()
        {
            var list = await _db.DebtTypes.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            return Json(new { ok = true, list });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert([FromBody] DebtType dto)
        {
            // dto: { Id, Name }
            if (dto == null) return BadRequest(new { ok = false, message = "Geçersiz istek." });

            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { ok = false, message = "Kategori adı zorunludur." });

            // aynı isimden var mı? (case-insensitive)
            var existsSameName = await _db.DebtTypes
                .AnyAsync(x => x.Id != dto.Id && x.Name.ToLower() == name.ToLower());

            if (existsSameName)
                return BadRequest(new { ok = false, message = "Bu isimde bir gider kategorisi zaten var." });

            if (dto.Id <= 0)
            {
                var ent = new IdeioMuhasebe.Entities.DebtType
                {
                    Name = name
                };

                _db.DebtTypes.Add(ent);
                await _db.SaveChangesAsync();

                return Json(new { ok = true, id = ent.Id });
            }
            else
            {
                var ent = await _db.DebtTypes.FirstOrDefaultAsync(x => x.Id == dto.Id);
                if (ent == null) return NotFound(new { ok = false, message = "Kayıt bulunamadı." });

                ent.Name = name;

                await _db.SaveChangesAsync();
                return Json(new { ok = true, id = ent.Id });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            var type = await _db.DebtTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (type == null)
                return NotFound(new { ok = false, message = "Kategori bulunamadı." });

            await using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                // 1) Bu kategoriye bağlı recurring rule id'leri
                var recurringIds = await _db.RecurringDebts
                    .Where(r => r.DebtTypeId == id)
                    .Select(r => r.Id)
                    .ToListAsync();

                // 2) Bu kategoriye bağlı borçları sil
                var debts = await _db.Debts.Where(x => x.DebtTypeId == id).ToListAsync();
                if (debts.Count > 0) _db.Debts.RemoveRange(debts);

                // 3) Varsa skip kayıtlarını sil (RecurringDebtSkips DbSet'in varsa)
                if (recurringIds.Count > 0)
                {
                    var skips = await _db.RecurringDebtSkips
                        .Where(s => recurringIds.Contains(s.RecurringDebtId))
                        .ToListAsync();
                    if (skips.Count > 0) _db.RecurringDebtSkips.RemoveRange(skips);
                }

                // 4) Recurring kuralları sil
                var rules = await _db.RecurringDebts.Where(r => r.DebtTypeId == id).ToListAsync();
                if (rules.Count > 0) _db.RecurringDebts.RemoveRange(rules);

                // 5) Kategoriyi sil
                _db.DebtTypes.Remove(type);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { ok = false, message = "Silinemedi: " + ex.Message });
            }
        }
    }
}