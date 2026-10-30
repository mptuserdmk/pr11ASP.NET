using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using pr3withDB.Authorization;
using pr3withDB.Models;
using pr3withDB.Services;
using Xunit;

namespace pr3withDB.Tests
{
    public class AuthAndPolicyTests
    {
        [Fact]
        public void PasswordHasher_HashesAndVerifiesPasswordCorrectly()
        {
            var hasher = new PasswordHasher<User>();
            var user = new User { IdUsers = 1, UserLogin = "testuser" };

            var hash = hasher.HashPassword(user, "SecurePass123!");
            Assert.False(string.IsNullOrWhiteSpace(hash));

            var successResult = hasher.VerifyHashedPassword(user, hash, "SecurePass123!");
            Assert.Equal(PasswordVerificationResult.Success, successResult);

            var failResult = hasher.VerifyHashedPassword(user, hash, "WrongPassword");
            Assert.Equal(PasswordVerificationResult.Failed, failResult);
        }

        [Fact]
        public void PasswordService_HashAndVerify_PasswordHasher_Works()
        {
            var hasher = new PasswordHasher<User>();
            var service = new PasswordService(hasher);
            var user = new User { IdUsers = 2, UserLogin = "newuser" };

            var hash = service.HashPassword(user, "StrongPass999!");
            user.UserPasswordHash = hash;

            // Проверяем правильный пароль
            var isCorrect = service.VerifyPassword(user, "StrongPass999!", out var needsRehash);
            Assert.True(isCorrect);
            Assert.False(needsRehash); // Уже в формате PasswordHasher, rehash не нужен

            // Проверяем неверный пароль
            var isWrong = service.VerifyPassword(user, "InvalidPass", out _);
            Assert.False(isWrong);
        }

        [Fact]
        public void PasswordService_Verify_LegacyBCrypt_WorksAndFlagsNeedsRehash()
        {
            var hasher = new PasswordHasher<User>();
            var service = new PasswordService(hasher);

            // Имитируем существующего пользователя со старым хешем BCrypt
            var user = new User
            {
                IdUsers = 3,
                UserLogin = "legacy_user",
                UserPasswordHash = BCrypt.Net.BCrypt.HashPassword("OldBcryptPassword123")
            };

            // Проверяем правильный пароль: должен успешно верифицироваться и установить флаг needsRehash = true
            var isCorrect = service.VerifyPassword(user, "OldBcryptPassword123", out var needsRehash);
            Assert.True(isCorrect);
            Assert.True(needsRehash);

            // Проверяем неверный пароль к хешу BCrypt: должен вернуть false
            var isWrong = service.VerifyPassword(user, "WrongPassword", out _);
            Assert.False(isWrong);
        }

        [Fact]
        public void PasswordService_Verify_CorruptedHash_ReturnsFalseWithoutThrowing()
        {
            var hasher = new PasswordHasher<User>();
            var service = new PasswordService(hasher);

            // Проверяем различные некорректные строки, которые раньше приводили к сбою BCrypt/Base64
            var user1 = new User { IdUsers = 4, UserLogin = "u1", UserPasswordHash = "$2a$invalid_salt_and_hash" };
            var user2 = new User { IdUsers = 5, UserLogin = "u2", UserPasswordHash = "NotBase64!!$#@!" };
            var user3 = new User { IdUsers = 6, UserLogin = "u3", UserPasswordHash = "" };

            Assert.False(service.VerifyPassword(user1, "anyPass", out _));
            Assert.False(service.VerifyPassword(user2, "anyPass", out _));
            Assert.False(service.VerifyPassword(user3, "anyPass", out _));
        }

        [Fact]
        public void JwtTokenService_GeneratesTokenWithRequiredClaims()
        {
            var configData = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "UnitTestingVerySecretKeyForJwt2026!MustBe32BytesLong",
                ["Jwt:Issuer"] = "ShopApiServer",
                ["Jwt:Audience"] = "ShopApiClient",
                ["Jwt:ExpiryMinutes"] = "30"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
            var service = new JwtTokenService(config);

            var user = new User
            {
                IdUsers = 77,
                UserLogin = "superadmin",
                IsActive = true,
                Role = new Role { RoleName = "Admin" }
            };

            var (token, expiration) = service.GenerateToken(user);

            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.True(expiration > DateTime.UtcNow);
        }

        [Fact]
        public async Task OrderAuthorizationHandler_AllowsAdmin_ForAnyOrder()
        {
            var handler = new OrderAuthorizationHandler();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "999"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("isActive", "true")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var requirement = new SameOwnerOrAdminRequirement();
            var order = new Order { IdOrders = 10, UserId = 42 }; // Чужой заказ

            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, order);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task OrderAuthorizationHandler_AllowsOwner_ForOwnOrder()
        {
            var handler = new OrderAuthorizationHandler();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim("isActive", "true")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var requirement = new SameOwnerOrAdminRequirement();
            var order = new Order { IdOrders = 10, UserId = 42 }; // Свой заказ

            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, order);

            await handler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task OrderAuthorizationHandler_DeniesNonOwner_ForOtherOrder()
        {
            var handler = new OrderAuthorizationHandler();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "55"), // Другой пользователь
                new Claim(ClaimTypes.Role, "User"),
                new Claim("isActive", "true")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var requirement = new SameOwnerOrAdminRequirement();
            var order = new Order { IdOrders = 10, UserId = 42 };

            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, order);

            await handler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task OrderAuthorizationHandler_DeniesDeactivatedUser()
        {
            var handler = new OrderAuthorizationHandler();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim("isActive", "false") // Заблокирован!
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var requirement = new SameOwnerOrAdminRequirement();
            var order = new Order { IdOrders = 10, UserId = 42 };

            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, order);

            await handler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }
    }
}
