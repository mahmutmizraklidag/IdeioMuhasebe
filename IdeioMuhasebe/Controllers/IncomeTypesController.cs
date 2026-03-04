using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdeioMuhasebe.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class IncomeTypesController : Controller
    {
        private readonly DatabaseContext _db;

        public IncomeTypesController(DatabaseContext db)
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
            var types = await _db.IncomeTypes.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            var nowMonth = MonthStart(DateTime.Today);

            var rules = await _db.RecurringIncomes.AsNoTracking()
                .Where(r => r.IsActive && r.PeriodCount.HasValue && r.PeriodCount.Value > 0)
                .Select(r => new { r.IncomeTypeId, r.StartDate, r.PeriodCount })
                .ToListAsync();

            var warnSet = new HashSet<int>();
            foreach (var r in rules)
            {
                var startMonth = MonthStart(r.StartDate);
                var endMonth = startMonth.AddMonths(r.PeriodCount.Value - 1);
                var left = MonthDiff(nowMonth, endMonth);
                if (left == 1) warnSet.Add(r.IncomeTypeId);
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
            var list = await _db.IncomeTypes.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name })
                .ToListAsync();

            return Json(new { ok = true, list });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert([FromBody] IdeioMuhasebe.Entities.IncomeType dto)
        {
            // dto: { Id, Name }
            if (dto == null) return BadRequest(new { ok = false, message = "Geçersiz istek." });

            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { ok = false, message = "Kategori adı zorunludur." });

            // aynı isimden var mı? (case-insensitive)
            var existsSameName = await _db.IncomeTypes
                .AnyAsync(x => x.Id != dto.Id && x.Name.ToLower() == name.ToLower());

            if (existsSameName)
                return BadRequest(new { ok = false, message = "Bu isimde bir gelir kategorisi zaten var." });

            if (dto.Id <= 0)
            {
                var ent = new IdeioMuhasebe.Entities.IncomeType
                {
                    Name = name
                };

                _db.IncomeTypes.Add(ent);
                await _db.SaveChangesAsync();

                return Json(new { ok = true, id = ent.Id });
            }
            else
            {
                var ent = await _db.IncomeTypes.FirstOrDefaultAsync(x => x.Id == dto.Id);
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
            var type = await _db.IncomeTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (type == null)
                return NotFound(new { ok = false, message = "Kategori bulunamadı." });

            await using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                // 1) Bu kategoriye bağlı recurring rule id'leri
                var recurringIds = await _db.RecurringIncomes
                    .Where(r => r.IncomeTypeId == id)
                    .Select(r => r.Id)
                    .ToListAsync();

                // 2) Bu kategoriye bağlı gelirleri sil
                var incomes = await _db.Incomes.Where(x => x.IncomeTypeId == id).ToListAsync();
                if (incomes.Count > 0) _db.Incomes.RemoveRange(incomes);

                // 3) Varsa skip kayıtlarını sil (RecurringIncomeSkips DbSet'in varsa)
                if (recurringIds.Count > 0)
                {
                    var skips = await _db.RecurringIncomeSkips
                        .Where(s => recurringIds.Contains(s.RecurringIncomeId))
                        .ToListAsync();
                    if (skips.Count > 0) _db.RecurringIncomeSkips.RemoveRange(skips);
                }

                // 4) Recurring kuralları sil
                var rules = await _db.RecurringIncomes.Where(r => r.IncomeTypeId == id).ToListAsync();
                if (rules.Count > 0) _db.RecurringIncomes.RemoveRange(rules);

                // 5) Kategoriyi sil
                _db.IncomeTypes.Remove(type);

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