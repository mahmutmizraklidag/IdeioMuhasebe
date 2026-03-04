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
    }
}