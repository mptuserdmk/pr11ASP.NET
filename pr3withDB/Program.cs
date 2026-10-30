using System;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using pr3withDB.Authorization;
using pr3withDB.Mapping;
using pr3withDB.Models;
using pr3withDB.Repositories;
using pr3withDB.Services;
using pr3withDB.Swagger;

var builder = WebApplication.CreateBuilder(args);

// MVC & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Swagger / OpenAPI с поддержкой JWT Bearer и динамических замков
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Shop API",
        Version = "v1",
        Description = "ASP.NET Core Web API с JWT-аутентификацией, Policy-based авторизацией, CORS и Rate Limiting"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization заголовок с использованием схемы Bearer. Пример: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    // Добавляет замочек ТОЛЬКО к реально защищенным [Authorize] эндпоинтам
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

// AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Repositories (Scoped)
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Services (Scoped)
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// PasswordHasher<User>
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Resource-Based Authorization Handlers
builder.Services.AddSingleton<IAuthorizationHandler, OrderAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CartAuthorizationHandler>();

// Database Context
builder.Services.AddDbContext<ShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS: Чтение списка разрешенных origin фронтенда из appsettings.json
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                     ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-Total-Count");
    });
});

// Rate Limiting с партиционированием по IP-адресу клиента на эндпоинте входа
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Превышен лимит запросов на вход (Rate Limit: 5 запросов в минуту). Повторите попытку позже.\",\"statusCode\":429}",
            cancellationToken: token);
    };

    options.AddPolicy("login-limiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// JWT & Cookie Authentication
var jwtKey = builder.Configuration["Jwt:Key"] 
             ?? "VeryStrongAndSecretKeyForJwtAuthenticationPr10AspShop2026!MustBe32BytesOrLonger";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ShopApiServer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ShopApiClient";

builder.Services.AddAuthentication(options =>
{
    // По умолчанию для API используется схема JWT Bearer
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Строго возвращаем JSON со статусами 401 и 403 (без 302 Redirect)
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"message\":\"Требуется валидный JWT access-токен (401 Unauthorized)\",\"statusCode\":401}");
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"message\":\"Недостаточно прав для доступа к ресурсу (403 Forbidden)\",\"statusCode\":403}");
        }
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // Политика проверки активности учетной записи
    options.AddPolicy("RequireActiveUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var isActiveClaim = ctx.User.FindFirst("isActive")?.Value;
            return string.Equals(isActiveClaim, "true", StringComparison.OrdinalIgnoreCase);
        });
    });

    // Role-based политика для администраторов (с обязательной проверкой IsActive)
    options.AddPolicy("RequireAdminRole", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
        policy.RequireAssertion(ctx =>
        {
            var isActiveClaim = ctx.User.FindFirst("isActive")?.Value;
            return string.Equals(isActiveClaim, "true", StringComparison.OrdinalIgnoreCase);
        });
    });

    // Resource-based политика проверки принадлежности ресурса владельцу либо Admin
    options.AddPolicy("OwnerOrAdminPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new SameOwnerOrAdminRequirement());
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// UTF-8 для Razor
builder.Services.AddSingleton(System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));

var app = builder.Build();

// Миграция структуры БД и сидирование пользователей
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    try
    {
        context.Database.ExecuteSqlRaw("IF COL_LENGTH('Products', 'product_image_url') IS NULL ALTER TABLE Products ADD product_image_url nvarchar(500) NULL");
    }
    catch { }

    try
    {
        context.Database.ExecuteSqlRaw("IF COL_LENGTH('CartItems', 'user_id') IS NULL ALTER TABLE CartItems ADD user_id nvarchar(450) NULL");
        context.Database.ExecuteSqlRaw("ALTER TABLE CartItems ALTER COLUMN cart_ID int NULL");
    }
    catch { }

    try
    {
        context.Database.ExecuteSqlRaw(@"
            BEGIN TRY
                ALTER TABLE Users ALTER COLUMN user_password_hash nvarchar(255) NOT NULL;
            END TRY
            BEGIN CATCH
                ALTER TABLE Users DROP COLUMN user_password_hash;
                ALTER TABLE Users ADD user_password_hash nvarchar(255) NOT NULL DEFAULT '';
            END CATCH
        ");
    }
    catch { }

    try
    {
        context.Database.ExecuteSqlRaw("IF COL_LENGTH('Users', 'is_active') IS NULL ALTER TABLE Users ADD is_active bit NOT NULL DEFAULT 1");
    }
    catch { }

    try
    {
        // 1. Гарантируем роли "User" и "Admin"
        if (!context.Roles.Any(r => r.RoleName == "User"))
        {
            context.Roles.Add(new Role { RoleName = "User" });
            context.SaveChanges();
        }

        if (!context.Roles.Any(r => r.RoleName == "Admin"))
        {
            context.Roles.Add(new Role { RoleName = "Admin" });
            context.SaveChanges();
        }

        var adminRole = context.Roles.First(r => r.RoleName == "Admin");
        var userRole = context.Roles.First(r => r.RoleName == "User");

        // 2. Сидирование / миграция хеша PasswordHasher<User> для admin (admin123)
        var adminUser = context.Users.FirstOrDefault(u => u.UserLogin == "admin");
        if (adminUser == null)
        {
            adminUser = new User
            {
                UserLogin = "admin",
                RoleId = adminRole.IdRoles,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty
            };
            adminUser.UserPasswordHash = passwordHasher.HashPassword(adminUser, "admin123");
            context.Users.Add(adminUser);
        }
        else
        {
            adminUser.UserPasswordHash = passwordHasher.HashPassword(adminUser, "admin123");
            adminUser.RoleId = adminRole.IdRoles;
            adminUser.IsActive = true;
        }

        // 3. Сидирование / миграция хеша PasswordHasher<User> для user (user123)
        var regularUser = context.Users.FirstOrDefault(u => u.UserLogin == "user");
        if (regularUser == null)
        {
            regularUser = new User
            {
                UserLogin = "user",
                RoleId = userRole.IdRoles,
                IsActive = true,
                UserCreatedAt = DateTime.UtcNow,
                UserPasswordHash = string.Empty
            };
            regularUser.UserPasswordHash = passwordHasher.HashPassword(regularUser, "user123");
            context.Users.Add(regularUser);
        }
        else
        {
            regularUser.UserPasswordHash = passwordHasher.HashPassword(regularUser, "user123");
            regularUser.RoleId = userRole.IdRoles;
            regularUser.IsActive = true;
        }

        context.SaveChanges();
    }
    catch { }
}

// Middleware: принудительный UTF-8 для HTML views
app.Use((context, next) => 
{ 
    if (!context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers.ContentType = "text/html; charset=utf-8"; 
    }
    return next(); 
});

// Swagger UI Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shop API v1");
    c.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// СТРОГИЙ ПОРЯДОК MIDDLEWARE:
// UseCors ОБЯЗАТЕЛЬНО ДО UseRateLimiter, UseAuthentication и UseAuthorization!
app.UseCors("FrontendCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllers();

app.MapControllerRoute(
    name: "admin",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Products}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

public partial class Program { }
