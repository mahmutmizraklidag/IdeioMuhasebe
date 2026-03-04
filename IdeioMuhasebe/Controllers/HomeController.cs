using IdeioMuhasebe.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly DatabaseContext _db;
        public HomeController(DatabaseContext db) => _db = db;

        public IActionResult Index() => View();

        private static (DateTime from, DateTime toExclusive) Range(DateTime? from, DateTime? to)
        {
            var f = (from ?? DateTime.Today).Date;
            var t = (to ?? DateTime.Today).Date;
            if (t < f) (f, t) = (t, f);
            return (f, t.AddDays(1));
        }

        // Dashboard: kartlar + yaklaşan giderler + yaklaşan gelirler (seçili aralığa göre)
        [HttpGet]
        public async Task<IActionResult> Summary(DateTime? from, DateTime? to, int? debtTypeId, int? incomeTypeId)
        {
            var (f, toEx) = Range(from, to);

            // EXPENSES (Debts)
            var qExp = _db.Debts.AsNoTracking()
                .Include(x => x.DebtType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (debtTypeId.HasValue && debtTypeId.Value > 0)
                qExp = qExp.Where(x => x.DebtTypeId == debtTypeId.Value);

            var expTotal = await qExp.SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var expPaid = await qExp.Where(x => x.IsPaid).SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var expRemaining = expTotal - expPaid;

            var upcomingExpenses = await qExp
                .Where(x => !x.IsPaid)
                .OrderBy(x => x.DueDate)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Amount,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    debtType = x.DebtType.Name,
                    debtTypeId = x.DebtTypeId,
                    x.IsPaid
                })
                .Take(50)
                .ToListAsync();

            // INCOMES
            var qInc = _db.Incomes.AsNoTracking()
                .Include(x => x.IncomeType)
                .Where(x => x.DueDate >= f && x.DueDate < toEx);

            if (incomeTypeId.HasValue && incomeTypeId.Value > 0)
                qInc = qInc.Where(x => x.IncomeTypeId == incomeTypeId.Value);

            var incTotal = await qInc.SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var incReceived = await qInc.Where(x => x.IsReceived).SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var incRemaining = incTotal - incReceived;

            var upcomingIncomes = await qInc
                .Where(x => !x.IsReceived)
                .OrderBy(x => x.DueDate)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Amount,
                    dueDate = x.DueDate.ToString("yyyy-MM-dd"),
                    incomeType = x.IncomeType.Name,
                    incomeTypeId = x.IncomeTypeId,
                    x.IsReceived
                })
                .Take(50)
                .ToListAsync();

            return Json(new
            {
                ok = true,
                range = new { from = f.ToString("yyyy-MM-dd"), to = toEx.AddDays(-1).ToString("yyyy-MM-dd") },
                cards = new
                {
                    expenseTotal = expTotal,
                    expensePaid = expPaid,
                    expenseRemaining = expRemaining,
                    incomeTotal = incTotal,
                    incomeReceived = incReceived,
                    incomeRemaining = incRemaining
                },
                upcomingExpenses,
                upcomingIncomes
            });
        }
    }
}