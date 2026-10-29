using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using pr3withDB.Common;
using pr3withDB.DTOs;
using pr3withDB.Models;

namespace pr3withDB.Services
{
    public class AuthService : IAuthService
    {
        private readonly ShopDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            ShopDbContext context,
            IPasswordHasher<User> passwordHasher,
            IJwtTokenService jwtTokenService,
            ILogger<AuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterDto dto)
        {
            if (dto == null)
            {
                return ServiceResult<AuthResponseDto>.Failed("Данные регистрации не переданы");
            }

            var trimmedLogin = dto.Login.Trim();
            var exists = await _context.Users.AnyAsync(u => u.UserLogin.ToLower() == trimmedLogin.ToLower());
            if (exists)
            {
                return ServiceResult<AuthResponseDto>.Failed($"Пользователь с логином '{trimmedLogin}' уже существует", ServiceErrorType.Conflict);
            }

            // Находим или создаем роль User
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            if (userRole == null)
            {
                userRole = new Role { RoleName = "User" };
                _context.Roles.Add(userRole);
                await _context.SaveChangesAsync();
            }

            var newUser = new User
            {
                UserLogin = trimmedLogin,
                RoleId = userRole.IdRoles,
                Role = userRole,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty // временно
            };

            // Хешируем через PasswordHasher<User>
            newUser.UserPasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Пользователь {Login} успешно зарегистрирован с ID {Id}", newUser.UserLogin, newUser.IdUsers);

            var (token, expiration) = _jwtTokenService.GenerateToken(newUser);

            return ServiceResult<AuthResponseDto>.Success(new AuthResponseDto
            {
                Token = token,
                Expiration = expiration,
                UserId = newUser.IdUsers,
                Login = newUser.UserLogin,
                Role = userRole.RoleName,
                IsActive = newUser.IsActive
            });
        }

        public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto)
        {
            if (dto == null)
            {
                return ServiceResult<AuthResponseDto>.Failed("Данные входа не переданы");
            }

            var trimmedLogin = dto.Login.Trim();
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserLogin.ToLower() == trimmedLogin.ToLower());

            if (user == null)
            {
                _logger.LogWarning("Неудачная попытка входа: пользователь {Login} не найден", trimmedLogin);
                return ServiceResult<AuthResponseDto>.Unauthorized("Неверный логин или пароль");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Попытка входа в деактивированную учетную запись {Login}", trimmedLogin);
                return ServiceResult<AuthResponseDto>.Forbidden("Учетная запись деактивирована администратором");
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.UserPasswordHash, dto.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Неудачная попытка входа: неверный пароль для пользователя {Login}", trimmedLogin);
                return ServiceResult<AuthResponseDto>.Unauthorized("Неверный логин или пароль");
            }

            // При успехе генерируем токен
            var (token, expiration) = _jwtTokenService.GenerateToken(user);

            _logger.LogInformation("Пользователь {Login} успешно вошел в систему", user.UserLogin);

            return ServiceResult<AuthResponseDto>.Success(new AuthResponseDto
            {
                Token = token,
                Expiration = expiration,
                UserId = user.IdUsers,
                Login = user.UserLogin,
                Role = user.Role?.RoleName ?? "User",
                IsActive = user.IsActive
            });
        }
    }
}

