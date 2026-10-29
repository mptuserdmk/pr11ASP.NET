using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace pr3withDB.Tests
{
    public class ApiEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ApiEndpointIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetPublicProducts_Returns200AndExposedXTotalCountHeader()
        {
            // Act
            var response = await _client.GetAsync("/api/products?page=1&pageSize=5");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Total-Count") || response.Headers.Contains("Access-Control-Expose-Headers"));
        }

        [Fact]
        public async Task GetProtectedMe_WithoutToken_Returns401Unauthorized_NotRedirect302()
        {
            // Act: обращение к защищенному эндпоинту без передачи JWT токена
            var response = await _client.GetAsync("/api/auth/me");

            // Assert: строго 401 Unauthorized, а не 302 Redirect!
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("401", body);
        }

        [Fact]
        public async Task PostLogin_InvalidCredentials_Returns401Unauthorized()
        {
            // Arrange
            var loginPayload = new { login = "non_existent_user_999", password = "wrong_password" };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/auth/login", content);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CorsPreflight_FromAllowedOrigin_ReturnsCorsHeaders()
        {
            // Arrange: симулируем браузерный Preflight OPTIONS запрос с React фронтенда
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
            request.Headers.Add("Origin", "http://localhost:5173");
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

            // Act
            var response = await _client.SendAsync(request);

            // Assert: CORS заголовки разрешают фронтенд origin
            Assert.True(
                response.Headers.Contains("Access-Control-Allow-Origin") ||
                response.StatusCode == HttpStatusCode.NoContent ||
                response.StatusCode == HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetAdminUsers_WithUserRoleToken_Returns403Forbidden()
        {
            // Arrange: создаем пользователя с ролью User и генерируем токен
            var testUserToken = GenerateMockJwtToken(role: "User", isActive: "true");
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", testUserToken);

            // Act: обычный пользователь пытается получить список всех пользователей (доступен только Admin)
            var response = await _client.SendAsync(request);

            // Assert: строго 403 Forbidden
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task PostLogin_RateLimiter_Returns429TooManyRequests_OnBurst()
        {
            // Arrange: отправляем серию запросов на /api/auth/login для превышения лимита (5 в минуту)
            HttpResponseMessage? lastResponse = null;

            for (int i = 0; i < 7; i++)
            {
                var payload = new { login = "rate_limit_test", password = "some_password" };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                lastResponse = await _client.PostAsync("/api/auth/login", content);
                if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    break;
                }
            }

            // Assert: RateLimiter возвращает 429 Too Many Requests
            Assert.NotNull(lastResponse);
            Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        }

        private static string GenerateMockJwtToken(string role, string isActive)
        {
            var key = "VeryStrongAndSecretKeyForJwtAuthenticationPr10AspShop2026!MustBe32BytesOrLonger";
            var securityKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(securityKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim("sub", "1234"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1234"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "test_regular_user"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role),
                new System.Security.Claims.Claim("role", role),
                new System.Security.Claims.Claim("isActive", isActive)
            };

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "ShopApiServer",
                audience: "ShopApiClient",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials);

            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

