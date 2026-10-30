using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using pr3withDB.Controllers;
using pr3withDB.DTOs;
using pr3withDB.Models;
using pr3withDB.Models.ViewModels;
using pr3withDB.Services;
using Xunit;

namespace pr3withDB.Tests
{
    public class TestUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; set; } = new();
        public string? Action(UrlActionContext actionContext) => $"/{actionContext.Controller ?? "Home"}/{actionContext.Action}";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
        public string? Link(string? routeName, object? values) => "/";
    }

    public class AccountLoginTests
    {
        private ShopDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ShopDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new ShopDbContext(options);
            return context;
        }

        private (AccountController Controller, ShopDbContext Context) CreateAccountController(string dbName)
        {
            var context = CreateInMemoryDbContext(dbName);
            var hasher = new PasswordHasher<User>();
            var passwordService = new PasswordService(hasher);

            var services = new ServiceCollection();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => { });
            services.AddLogging();
            services.AddControllersWithViews();
            var serviceProvider = services.BuildServiceProvider();

            var actionContext = new ActionContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = serviceProvider
                },
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            };

            var controller = new AccountController(context, passwordService)
            {
                ControllerContext = new ControllerContext(actionContext),
                Url = new TestUrlHelper()
            };

            return (controller, context);
        }

        private (AuthService Service, ShopDbContext Context) CreateAuthService(string dbName)
        {
            var context = CreateInMemoryDbContext(dbName);
            var hasher = new PasswordHasher<User>();
            var passwordService = new PasswordService(hasher);

            var configData = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretKeyForTestingJwtAuthentication32BytesLong!",
                ["Jwt:Issuer"] = "ShopApiServer",
                ["Jwt:Audience"] = "ShopApiClient",
                ["Jwt:ExpiryMinutes"] = "60"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
            var jwtTokenService = new JwtTokenService(config);

            var authService = new AuthService(
                context,
                passwordService,
                jwtTokenService,
                NullLogger<AuthService>.Instance);

            return (authService, context);
        }

        [Fact]
        public async Task AccountController_Login_WithPasswordHasherHash_SucceedsWithoutBcryptCrash()
        {
            // Arrange: Пользователь с хешем PasswordHasher<User> (как admin после сидирования)
            var (controller, context) = CreateAccountController(nameof(AccountController_Login_WithPasswordHasherHash_SucceedsWithoutBcryptCrash));
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                IdUsers = 1,
                UserLogin = "admin",
                RoleId = 1,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty,
                Role = new Role { IdRoles = 1, RoleName = "Admin" }
            };
            user.UserPasswordHash = hasher.HashPassword(user, "admin123");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var model = new LoginViewModel
            {
                Login = "admin",
                Password = "admin123",
                RememberMe = false
            };

            // Act: Вход через AccountController
            var result = await controller.Login(model);

            // Assert: Успешный редирект, никакого SaltParseException / ошибки BCrypt!
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Products", redirectResult.ControllerName);
        }

        [Fact]
        public async Task AccountController_Login_WithLegacyBcryptHash_SucceedsAndAutoRehashesToPasswordHasher()
        {
            // Arrange: Пользователь со старым хешем BCrypt
            var (controller, context) = CreateAccountController(nameof(AccountController_Login_WithLegacyBcryptHash_SucceedsAndAutoRehashesToPasswordHasher));

            var legacyBcryptHash = BCrypt.Net.BCrypt.HashPassword("mypassword123");
            Assert.StartsWith("$2", legacyBcryptHash);

            var user = new User
            {
                IdUsers = 2,
                UserLogin = "legacy_user",
                RoleId = 2,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = legacyBcryptHash,
                Role = new Role { IdRoles = 2, RoleName = "User" }
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var model = new LoginViewModel
            {
                Login = "legacy_user",
                Password = "mypassword123",
                RememberMe = false
            };

            // Act
            var result = await controller.Login(model);

            // Assert: Успешный вход
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);

            // Проверяем автоматическое обновление хеша в БД на формат PasswordHasher<User> (начинается с AQAAAA)
            var updatedUser = await context.Users.FindAsync(2);
            Assert.NotNull(updatedUser);
            Assert.False(updatedUser.UserPasswordHash.StartsWith("$2"), "Хеш должен был обновиться с BCrypt на PasswordHasher");
            Assert.StartsWith("AQAAAA", updatedUser.UserPasswordHash);
        }

        [Fact]
        public async Task AccountController_Login_WithWrongPassword_ReturnsViewWithErrorAndNo500()
        {
            var (controller, context) = CreateAccountController(nameof(AccountController_Login_WithWrongPassword_ReturnsViewWithErrorAndNo500));
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                IdUsers = 3,
                UserLogin = "some_user",
                RoleId = 2,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty,
                Role = new Role { IdRoles = 2, RoleName = "User" }
            };
            user.UserPasswordHash = hasher.HashPassword(user, "correctPassword");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var model = new LoginViewModel
            {
                Login = "some_user",
                Password = "wrongPassword",
                RememberMe = false
            };

            var result = await controller.Login(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
        }

        [Fact]
        public async Task AccountController_Register_CreatesUserWithPasswordService_AndCanLogin()
        {
            var (controller, context) = CreateAccountController(nameof(AccountController_Register_CreatesUserWithPasswordService_AndCanLogin));
            context.Roles.Add(new Role { IdRoles = 1, RoleName = "User" });
            await context.SaveChangesAsync();

            var registerModel = new RegisterViewModel
            {
                Login = "new_user",
                Password = "newPassword123",
                ConfirmPassword = "newPassword123"
            };

            var registerResult = await controller.Register(registerModel);
            var redirectResult = Assert.IsType<RedirectToActionResult>(registerResult);
            Assert.Equal("Login", redirectResult.ActionName);

            var createdUser = await context.Users.FirstOrDefaultAsync(u => u.UserLogin == "new_user");
            Assert.NotNull(createdUser);
            Assert.StartsWith("AQAAAA", createdUser.UserPasswordHash);

            // Теперь входим под этим пользователем
            var loginResult = await controller.Login(new LoginViewModel
            {
                Login = "new_user",
                Password = "newPassword123"
            });

            var loginRedirect = Assert.IsType<RedirectToActionResult>(loginResult);
            Assert.Equal("Index", loginRedirect.ActionName);
        }

        [Fact]
        public async Task AuthService_Login_WithLegacyBcryptHash_SucceedsAndAutoRehashes()
        {
            var (authService, context) = CreateAuthService(nameof(AuthService_Login_WithLegacyBcryptHash_SucceedsAndAutoRehashes));

            var legacyBcryptHash = BCrypt.Net.BCrypt.HashPassword("bcryptSecretPass");
            var user = new User
            {
                IdUsers = 10,
                UserLogin = "api_legacy_user",
                RoleId = 1,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = legacyBcryptHash,
                Role = new Role { IdRoles = 1, RoleName = "User" }
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Login = "api_legacy_user",
                Password = "bcryptSecretPass"
            };

            var result = await authService.LoginAsync(loginDto);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));

            // Проверяем, что в базе данных хеш автоматически мигрировал на PasswordHasher
            var userInDb = await context.Users.FindAsync(10);
            Assert.NotNull(userInDb);
            Assert.StartsWith("AQAAAA", userInDb.UserPasswordHash);
        }

        [Fact]
        public async Task AuthService_Login_WithPasswordHasherHash_Succeeds()
        {
            var (authService, context) = CreateAuthService(nameof(AuthService_Login_WithPasswordHasherHash_Succeeds));
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                IdUsers = 11,
                UserLogin = "api_modern_user",
                RoleId = 2,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty,
                Role = new Role { IdRoles = 2, RoleName = "Admin" }
            };
            user.UserPasswordHash = hasher.HashPassword(user, "adminSecret123");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Login = "api_modern_user",
                Password = "adminSecret123"
            };

            var result = await authService.LoginAsync(loginDto);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Admin", result.Data.Role);
            Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        }

        [Fact]
        public async Task AuthService_Login_WithWrongPassword_ReturnsUnauthorized()
        {
            var (authService, context) = CreateAuthService(nameof(AuthService_Login_WithWrongPassword_ReturnsUnauthorized));
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                IdUsers = 12,
                UserLogin = "user_wrong_pass",
                RoleId = 1,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty,
                Role = new Role { IdRoles = 1, RoleName = "User" }
            };
            user.UserPasswordHash = hasher.HashPassword(user, "realPassword");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Login = "user_wrong_pass",
                Password = "incorrectPassword"
            };

            var result = await authService.LoginAsync(loginDto);

            Assert.False(result.IsSuccess);
            Assert.Equal(Common.ServiceErrorType.Unauthorized, result.ErrorType);
        }
    }
}
