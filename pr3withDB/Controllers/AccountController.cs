using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using pr3withDB.Models;
using pr3withDB.Models.ViewModels;
using pr3withDB.Services;

namespace pr3withDB.Controllers
{
    public class AccountController : Controller
    {
        private readonly ShopDbContext _context;
        private readonly IPasswordService _passwordService;

        public AccountController(ShopDbContext context, IPasswordService passwordService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserLogin == model.Login);

                if (user != null)
                {
                    // Безопасная проверка пароля (поддерживает и PasswordHasher, и legacy BCrypt)
                    if (_passwordService.VerifyPassword(user, model.Password, out var needsRehash))
                    {
                        if (needsRehash)
                        {
                            user.UserPasswordHash = _passwordService.HashPassword(user, model.Password);
                            await _context.SaveChangesAsync();
                        }

                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, user.IdUsers.ToString()),
                            new Claim(ClaimTypes.Name, user.UserLogin),
                            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "User")
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }

                        return RedirectToAction("Index", "Products");
                    }
                }
                ModelState.AddModelError("", "Неверный логин или пароль");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserLogin == model.Login);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Login", "Этот логин уже занят");
                    return View(model);
                }

                // Default role - assume "User"
                var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
                int roleId = defaultRole?.IdRoles ?? 1;

                var user = new User
                {
                    UserLogin = model.Login,
                    RoleId = roleId,
                    IsActive = true,
                    UserCreatedAt = DateTime.UtcNow,
                    UserPasswordHash = string.Empty
                };

                user.UserPasswordHash = _passwordService.HashPassword(user, model.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return RedirectToAction("Login");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Products");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
