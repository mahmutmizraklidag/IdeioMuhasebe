using IdeioMuhasebe.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class DebtTypeDetailApiController : Controller
    {
        private readonly DatabaseContext _db;
        public DebtTypeDetailApiController(DatabaseContext db) => _db = db;

        private static (DateTime from, DateTime toExclusive) Range(DateTime? from, DateTime? to)
        {
            var t = DateTime.Today;
            var defFrom = new DateTime(t.Year, t.Month, 1);
            var defTo = defFrom.AddMonths(1).AddDays(-1);

            var f = (from ?? defFrom).Date;
            var tt = (to ?? defTo).Date;
            if (tt < f) tt = f;
            return (f, tt.AddDays(1));
        }

        [HttpGet]
        public async Task<IActionResult> Data(int debtTypeId, DateTime? from, DateTime? to)
        {
            var (f, toEx) = Range(from, to);

            var q = _db.Debts.AsNoTracking()
                .Where(x => x.DebtTypeId == debtTypeId && x.DueDate >= f && x.DueDate < toEx);

            var total = await q.SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var paid = await q.Where(x => x.IsPaid).SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var remaining = total - paid;

            var list = await q.OrderBy(x => x.DueDate)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Amount,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    x.Payee,
                    x.IsPaid
                })
                .ToListAsync();

            return Json(new { ok = true, cards = new { total, paid, remaining }, list });
        }
    }
}