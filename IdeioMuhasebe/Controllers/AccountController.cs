using System.Security.Claims;
using IdeioMuhasebe.Data;
using IdeioMuhasebe.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseContext _db;
        public AccountController(DatabaseContext db) => _db = db;

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.LayoutOff = true;
            return View(new LoginVm());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVm vm)
        {
            ViewBag.LayoutOff = true;
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username == vm.Username);

            if (user == null || user.password != vm.Password)
            {
                ModelState.AddModelError("", "Kullanıcı adı veya şifre hatalı.");
                return View(vm);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}