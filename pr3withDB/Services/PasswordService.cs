using System;
using Microsoft.AspNetCore.Identity;
using pr3withDB.Models;

namespace pr3withDB.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly IPasswordHasher<User> _passwordHasher;

        public PasswordService(IPasswordHasher<User> passwordHasher)
        {
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        public string HashPassword(User user, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Пароль не может быть пустым.", nameof(password));
            }
            return _passwordHasher.HashPassword(user ?? new User(), password);
        }

        public bool VerifyPassword(User user, string password, out bool needsRehash)
        {
            needsRehash = false;
            if (user == null || string.IsNullOrWhiteSpace(user.UserPasswordHash) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            var hash = user.UserPasswordHash.Trim();

            // 1. Проверяем, является ли сохраненный хеш форматом BCrypt ($2a$, $2b$, $2y$, $2x$)
            if (hash.StartsWith("$2"))
            {
                try
                {
                    if (BCrypt.Net.BCrypt.Verify(password, hash))
                    {
                        // Пароль верный, но хеш устаревший (BCrypt) — требуется перехеширование в PasswordHasher<User>
                        needsRehash = true;
                        return true;
                    }
                    return false;
                }
                catch
                {
                    // Если хеш поврежден, не роняем приложение исключением
                    return false;
                }
            }

            // 2. Проверяем стандартным PasswordHasher<User> (Identity V3)
            try
            {
                var result = _passwordHasher.VerifyHashedPassword(user, hash, password);
                if (result == PasswordVerificationResult.Success)
                {
                    return true;
                }
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    needsRehash = true;
                    return true;
                }
                return false;
            }
            catch
            {
                // Если хеш поврежден или содержит не-base64 символы, возвращаем false вместо исключения
                return false;
            }
        }
    }
}

