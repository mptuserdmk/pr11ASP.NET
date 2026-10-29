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

