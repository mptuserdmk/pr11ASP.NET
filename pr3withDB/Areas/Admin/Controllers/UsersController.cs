using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using pr3withDB.Models;
using pr3withDB.Services;

namespace pr3withDB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ShopDbContext _context;
        private readonly IPasswordService _passwordService;

        public UsersController(ShopDbContext context, IPasswordService passwordService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        // Список
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.Include(u => u.Role).ToListAsync();
            return View(users);
        }

        // Создание (Форма)
        public IActionResult Create()
        {
            ViewBag.RoleId = new SelectList(_context.Roles, "IdRoles", "RoleName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            if (ModelState.IsValid)
            {
                // Хеширование пароля через PasswordService
                user.UserPasswordHash = _passwordService.HashPassword(user, user.Password ?? string.Empty);
                user.UserCreatedAt = DateTime.UtcNow;
                user.IsActive = true;

                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.RoleId = new SelectList(_context.Roles, "IdRoles", "RoleName", user.RoleId);
            return View(user);
        }

        // Редактирование (Форма)
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            ViewBag.RoleId = new SelectList(_context.Roles, "IdRoles", "RoleName", user.RoleId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User user)
        {
            // Для редактирования пароль необязателен, если мы его не меняем
            if (string.IsNullOrEmpty(user.Password))
            {
                ModelState.Remove("Password");
            }

            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsers == user.IdUsers);
                if (existingUser == null) return NotFound();

                if (!string.IsNullOrEmpty(user.Password))
                {
                    user.UserPasswordHash = _passwordService.HashPassword(user, user.Password);
                }
                else
                {
                    user.UserPasswordHash = existingUser.UserPasswordHash;
                }
                
                user.UserCreatedAt = existingUser.UserCreatedAt; // Сохраняем дату создания

                _context.Update(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.RoleId = new SelectList(_context.Roles, "IdRoles", "RoleName", user.RoleId);
            return View(user);
        }

        // Удаление
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null) _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
