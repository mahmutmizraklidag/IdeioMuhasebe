using System.Security.Claims;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Entities;
using IdeioMuhasebe.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly DatabaseContext _db;
        public UsersController(DatabaseContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentUsername => User.Identity?.Name ?? "";

       

        public IActionResult Index()
        {
           
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
          

            var list = await _db.Users.AsNoTracking()
                .OrderBy(x => x.Username)
                .Select(x => new { x.Id, x.Username })
                .ToListAsync();

            return Json(new { ok = true, list });
        }

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] UserUpsertVm vm)
        {
           

            if (string.IsNullOrWhiteSpace(vm.Username))
                return BadRequest(new { ok = false, message = "Kullanıcı adı zorunludur." });

            vm.Username = vm.Username.Trim();

            // yeni kullanıcı ise şifre zorunlu
            if (vm.Id == 0 && string.IsNullOrWhiteSpace(vm.Password))
                return BadRequest(new { ok = false, message = "Şifre zorunludur." });

            try
            {
                if (vm.Id == 0)
                {
                    var ent = new User
                    {
                        Username = vm.Username,
                        password = (vm.Password ?? "").Trim()
                    };
                    _db.Users.Add(ent);
                    await _db.SaveChangesAsync();
                    return Json(new { ok = true, id = ent.Id });
                }
                else
                {
                    var ent = await _db.Users.FirstOrDefaultAsync(x => x.Id == vm.Id);
                    if (ent == null) return NotFound();

                    // admin kullanıcı adını değiştirmeyi istersen kaldırabilirsin
                    if (string.Equals(ent.Username, "admin", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(vm.Username, "admin", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { ok = false, message = "admin kullanıcı adı değiştirilemez." });
                    }

                    ent.Username = vm.Username;

                    // şifre boş gelirse değişmesin
                    if (!string.IsNullOrWhiteSpace(vm.Password))
                        ent.password = vm.Password.Trim();

                    await _db.SaveChangesAsync();
                    return Json(new { ok = true });
                }
            }
            catch (DbUpdateException)
            {
                // Username unique index çakışması vb.
                return BadRequest(new { ok = false, message = "Bu kullanıcı adı zaten kullanılıyor." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            

            if (id == CurrentUserId)
                return BadRequest(new { ok = false, message = "Kendi kullanıcı hesabınızı silemezsiniz." });

            var ent = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (ent == null) return NotFound();

            if (string.Equals(ent.Username, "admin", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { ok = false, message = "admin silinemez." });

            _db.Users.Remove(ent);
            await _db.SaveChangesAsync();

            return Json(new { ok = true });
        }
    }
}