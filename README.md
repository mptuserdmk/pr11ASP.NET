# Практическая работа №11
## JWT-аутентификация, Policy-Based авторизация, CORS, Rate Limiting и клиентский фронтенд на React в ASP.NET Core 8.0

Данный репозиторий представляет собой веб-приложение интернет-магазина на платформе **ASP.NET Core 8.0** со слоистой архитектурой (*Controllers / Services / Repositories / AutoMapper / EF Core*), расширенное полноценной системой безопасности: **JWT Bearer аутентификацией**, **Role-Based** и **Resource-Based политиками авторизации**, **CORS**, защитой от перебора через **Rate Limiting**, селективной документацией **Swagger/OpenAPI** и отдельным клиентским приложением на **React (Vite + TypeScript)**.

---

## 🚀 Основной стек технологий
* **Платформа:** .NET 8.0 (C# 12)
* **Веб-фреймворк:** ASP.NET Core 8.0 (Web API + MVC / Razor Views)
* **База данных:** MS SQL Server (LocalDB / Express)
* **ORM:** Entity Framework Core 8.0 (`Microsoft.EntityFrameworkCore.SqlServer`, `Tools`, `Design`)
* **Аутентификация:** `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.0, Cookie Authentication (для Razor)
* **Хеширование паролей:** `Microsoft.AspNetCore.Identity.PasswordHasher<User>`
* **Ограничение запросов (Rate Limiting):** `Microsoft.AspNetCore.RateLimiting` (.NET 8 built-in)
* **Маппинг DTO:** `AutoMapper` 16.2.0 (`MappingProfile`)
* **Документация API:** Swagger / OpenAPI (`Swashbuckle.AspNetCore` 10.2.3) с фильтром `AuthorizeCheckOperationFilter`
* **Тестирование:** xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing` 8.0, `Microsoft.EntityFrameworkCore.InMemory` 8.0 (32 теста)
* **Клиентский фронтенд:** React 18, TypeScript, Vite 5 (Origin: `http://localhost:5173`)

---

## 📋 Что сделано в рамках практической работы №11

### 1. Сущность пользователя и безопасное хеширование (`PasswordHasher<T>`)
* В модель `User` (`pr3withDB/Models/User.cs`) добавлено поле активности `public bool IsActive { get; set; } = true;`.
* В `ShopDbContext.cs` настроен маппинг на колонку `is_active bit NOT NULL DEFAULT 1`.
* В `Program.cs` добавлен автоматический SQL-скрипт проверки и добавления столбца в реальную БД при запуске.
* Реализовано хеширование паролей без хранения в открытом виде через стандартный `Microsoft.AspNetCore.Identity.PasswordHasher<User>`.
* **Автоматическая миграция тестовых учетных записей**: При старте приложения в `Program.cs` пользователи `admin` (`admin123`) и `user` (`user123`) автоматически перезаписываются новыми валидными хешами `PasswordHasher<User>`.

### 2. JWT Bearer аутентификация и разделение схем
* Подключен `AddJwtBearer` с валидацией `Issuer`, `Audience`, `Lifetime`, `IssuerSigningKey` (HmacSha256) и `ClockSkew = TimeSpan.Zero`.
* Схема `JwtBearerDefaults.AuthenticationScheme` установлена схемой по умолчанию для API.
* При неавторизованном доступе к API возвращается строго статус `401 Unauthorized` с JSON-ответом (без редиректа 302 на MVC-страницу логина).
* Созданы DTO: `RegisterDto`, `LoginDto`, `AuthResponseDto`.
* Разработаны сервисы:
  * `IJwtTokenService` / `JwtTokenService`: генерация токена с клеймами `sub` / `NameIdentifier` (ID пользователя), `name` (логин), `role`, `isActive`, `jti`.
  * `IAuthService` / `AuthService`: регистрация новых пользователей, верификация хеша через `PasswordHasher<User>`, проверка активности учетной записи.
* В `AuthApiController` (`/api/auth`) реализованы методы:
  * `POST /api/auth/register` — регистрация нового пользователя (роль `User`, `IsActive = true`, возврат токена).
  * `POST /api/auth/login` — вход по логину/паролю (возврат токена).
  * `GET /api/auth/me` — получение профиля текущего пользователя по JWT-токену.

### 3. Policy-Based авторизация (Role-based & Resource-based)
* **Проверка активности**: В политики авторизации добавлена проверка claim `isActive == "true"`. Заблокированный пользователь не может выполнять запросы даже с неистекшим токеном.
* **Role-Based Policy (`RequireAdminRole`)**:
  * Доступ к административным операциям каталога (`POST/PUT/PATCH/DELETE` в `ProductsApiController`) и пользователей (`GET/POST/DELETE` в `UsersApiController`) открыт только для пользователей с ролью `Admin`.
  * Обычный пользователь (`User`) при попытке доступа получает `403 Forbidden`.
* **Resource-Based Policy (`OwnerOrAdminPolicy`)**:
  * Реализовано требование `SameOwnerOrAdminRequirement : IAuthorizationRequirement`.
  * Разработаны обработчики `OrderAuthorizationHandler` и `CartAuthorizationHandler` (`AuthorizationHandler<SameOwnerOrAdminRequirement, TResource>`).
  * Логика доступа:
    * Администратор (`Admin`) имеет доступ к **любому** ресурсу.
    * Обычный пользователь (`User`) имеет доступ **только к своему собственному ресурсу** (`order.UserId == currentUserId`). При попытке доступа к чужому заказу или корзине возвращается статус `403 Forbidden`.

### 4. CORS и экспонирование заголовков
* В `appsettings.json` задана секция `Cors:AllowedOrigins` (`http://localhost:5173`, `http://localhost:3000`).
* В `Program.cs` настроена именованная политика `"FrontendCorsPolicy"`.
* Использован метод `WithExposedHeaders("X-Total-Count")`, позволяющий клиентскому JavaScript читать заголовок пагинации `X-Total-Count`.

### 5. Rate Limiting на эндпоинте входа
* Подключен `AddRateLimiter` с именованной политикой `"login-limiter"` на базе `RateLimitPartition.GetFixedWindowLimiter`.
* **Партиционирование по IP**: Лимит в 5 запросов в минуту считается индивидуально для каждого клиента по `httpContext.Connection.RemoteIpAddress`.
* При превышении лимита возвращается статус `429 Too Many Requests` с информативным JSON-сообщением.
* **Строгий порядок Middleware в `Program.cs`**:
  ```csharp
  app.UseCors("FrontendCorsPolicy"); // Размещен ДО UseRateLimiter, чтобы 429 содержал CORS-заголовки
  app.UseRateLimiter();
  app.UseAuthentication();
  app.UseAuthorization();
  ```

### 6. Swagger UI с динамическими замками
* Подключен `AuthorizeCheckOperationFilter : IOperationFilter`.
* Иконка замка и требование Bearer-авторизации отображаются **только** у методов, помеченных атрибутом `[Authorize]` (и не имеющих `[AllowAnonymous]`).
* Общедоступные методы каталога и авторизации отображаются без замков.

### 7. Отдельный клиентский фронтенд на React (`client-app`)
* Полноценное SPA-приложение на React (Vite + TypeScript) с запуском на Origin `http://localhost:5173`.
* **Форма входа и регистрации**: Поля логина и пароля, кнопки быстрой подстановки (`admin / admin123`, `user / user123`), сохранение токена в `localStorage`, кнопка "Испортить токен" для проверки ошибок.
* **Интерактивный тестовый пульт (Testing Sandbox)** с проверкой всех 6 сценариев:
  * **Тест 1 (200 OK — свой ресурс)**: Запрос `GET /api/auth/me` с текущим токеном.
  * **Тест 2 (403 Forbidden — чужой ресурс)**: Запрос `GET /api/orders/9999` через Resource-based policy.
  * **Тест 3 (403 Forbidden — Role-based policy)**: Запрос `GET /api/users` (403 для `user`, 200 для `admin`).
  * **Тест 4 (401 Unauthorized)**: Запрос к защищенному эндпоинту без токена.
  * **Тест 5 (429 Too Many Requests)**: Быстрая отправка серии из 6 запросов на вход для демонстрации Rate Limiting.
  * **Тест 6 (CORS + Exposed Header)**: Запрос `GET /api/products` с чтением `response.headers.get('X-Total-Count')`.
* **Журнал ответов API**: Отображение времени, метода, URL, статуса (цветные бейджи 200/401/403/429), заголовков и JSON ответа.

### 8. Модульные и интеграционные тесты (`pr3withDB.Tests`)
Всего в проекте **32 автоматизированных теста** (100% пройдены):
* `ApiEndpointIntegrationTests` (6 интеграционных тестов через `WebApplicationFactory` и `HttpClient`):
  * `GetProtectedMe_WithoutToken_Returns401Unauthorized_NotRedirect302`
  * `PostLogin_InvalidCredentials_Returns401Unauthorized`
  * `GetAdminUsers_WithUserRoleToken_Returns403Forbidden`
  * `PostLogin_RateLimiter_Returns429TooManyRequests_OnBurst`
  * `CorsPreflight_FromAllowedOrigin_ReturnsCorsHeaders`
  * `GetPublicProducts_Returns200AndExposedXTotalCountHeader`
* `AuthAndPolicyTests` (9 тестов авторизации, безопасности и хеширования паролей):
  * `PasswordHasher_HashesAndVerifiesPasswordCorrectly`
  * `JwtTokenService_GeneratesTokenWithRequiredClaims`
  * `OrderAuthorizationHandler_AllowsAdmin_ForAnyOrder`
  * `OrderAuthorizationHandler_AllowsOwner_ForOwnOrder`
  * `OrderAuthorizationHandler_DeniesNonOwner_ForOtherOrder`
  * `OrderAuthorizationHandler_DeniesDeactivatedUser`
  * `PasswordService_HashAndVerify_PasswordHasher_Works`
  * `PasswordService_Verify_LegacyBCrypt_WorksAndFlagsNeedsRehash`
  * `PasswordService_Verify_CorruptedHash_ReturnsFalseWithoutThrowing`
* `AccountLoginTests` (7 интеграционных тестов входа через EF Core InMemory):
  * `AccountController_Login_WithPasswordHasherHash_SucceedsWithoutBcryptCrash`
  * `AccountController_Login_WithLegacyBcryptHash_SucceedsAndAutoRehashesToPasswordHasher`
  * `AccountController_Login_WithWrongPassword_ReturnsViewWithErrorAndNo500`
  * `AccountController_Register_CreatesUserWithPasswordService_AndCanLogin`
  * `AuthService_Login_WithLegacyBcryptHash_SucceedsAndAutoRehashes`
  * `AuthService_Login_WithPasswordHasherHash_Succeeds`
  * `AuthService_Login_WithWrongPassword_ReturnsUnauthorized`
* `MappingProfileTests` (10 тестов маппинга AutoMapper).

---

## 🛠️ Запуск проекта

### 1. Запуск Backend API (ASP.NET Core)
```powershell
# Сборка решения
dotnet build pr11ASP.NET.sln

# Запуск веб-сервера
dotnet run --project pr3withDB/pr3withDB.csproj
```
* **Swagger UI:** `http://localhost:5019/swagger`
* **Тестовые учетные данные:**
  * Администратор: логин `admin`, пароль `admin123` (роль `Admin`)
  * Пользователь: логин `user`, пароль `user123` (роль `User`)

### 2. Запуск клиентского фронтенда (React)
```powershell
cd client-app
npm install
npm run dev
```
* Откройте в браузере: `http://localhost:5173`

### 3. Запуск тестов
```powershell
dotnet test pr11ASP.NET.sln
```

---

## 🐛 Исправления и доработки (Bugfix)

### Исправление краша BCrypt при входе
**Проблема:** При попытке войти через MVC-форму или JWT API возникала ошибка `BCrypt.Net.SaltParseException` / `FormatException`, поскольку `PasswordHasher<User>.VerifyHashedPassword()` не умеет обрабатывать устаревшие хеши BCrypt (начинающиеся с `$2a$`).

**Решение:** Реализован сервис `PasswordService` (`pr3withDB/Services/PasswordService.cs`) с интерфейсом `IPasswordService`:
- Если хеш начинается с `$2` — проверяет через `BCrypt.Net.BCrypt.Verify()` и устанавливает флаг `needsRehash = true`
- Если хеш в формате `PasswordHasher<User>` — проверяет через штатный `IPasswordHasher<User>`
- При любом повреждённом хеше — возвращает `false` без исключений
- При успешном входе со старым BCrypt-хешем **автоматически перехеширует** пароль в современный формат `PasswordHasher<User>` и сохраняет в БД

**Файлы изменены:**
- `pr3withDB/Services/IPasswordService.cs` *(новый)*
- `pr3withDB/Services/PasswordService.cs` *(новый)*
- `pr3withDB/Services/AuthService.cs`
- `pr3withDB/Controllers/AccountController.cs`
- `pr3withDB/Areas/Admin/Controllers/UsersController.cs`
- `pr3withDB/DTOs/UserMapper.cs`
- `pr3withDB/Program.cs` — регистрация `IPasswordService` в DI
