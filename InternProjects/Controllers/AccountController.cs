using InternProjects.Data;
using InternProjects.Models;
using InternProjects.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternProjects.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AppDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Login()
        {
            // Влязъл потребител не бива да вижда формата за вход. Бисквитката живее
            // 8 часа (30 дни при "Запомни ме"), а началният маршрут е Account/Login,
            // затова без тази проверка при повторно отваряне на приложението се
            // показва формата, но с активна сесия - тоест с цялото меню и името.
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);


            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Грешен имейл или парола.");
                return View(model);
            }

            if (user.Status != "Активен")
            {
                ModelState.AddModelError("", "Профилът е деактивиран. Свържи се с администратор.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : null
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity), authProperties);

            user.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            return user.Role switch
            {
                "Admin" => RedirectToAction("Admin", "Dashboard"),
                _ => RedirectToAction("Intern", "Dashboard")
            };
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            bool emailTaken = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (emailTaken)
                ModelState.AddModelError(nameof(model.Email), "Потребител с този имейл вече съществува.");

            if (!ModelState.IsValid) return View(model);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber ?? "",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Role = "Intern",
                    Status = "Активен",
                    CreationDate = DateTime.Now
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _context.Interns.Add(new Intern
                {
                    UserId = user.Id,
                    University = model.University,
                    Specialty = model.Specialty,
                    StartDate = DateTime.Today,
                    TotalHours = 240,
                    TaskHours = 0,
                    AddedHours = 0,
                    ReportedHours = 0,
                    RemainingHours = 240
                });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Registration failed for {Email}", model.Email);
                ModelState.AddModelError("", "Грешка при регистрацията. Нищо не е записано - опитай пак.");
                return View(model);
            }

            TempData["Success"] = "Регистрацията е успешна. Влез с имейла и паролата си.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult Denied() => View();

        /// <summary>
        /// Насочва вече влязъл потребител към неговото табло според ролята.
        /// </summary>
        private IActionResult RedirectToDashboard() =>
            User.IsInRole("Admin")
                ? RedirectToAction("Admin", "Dashboard")
                : RedirectToAction("Intern", "Dashboard");
    }
}