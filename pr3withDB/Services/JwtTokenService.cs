using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using pr3withDB.Models;

namespace pr3withDB.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public (string Token, DateTime Expiration) GenerateToken(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var key = _configuration["Jwt:Key"] 
                ?? throw new InvalidOperationException("JWT Secret Key is not configured in appsettings.json.");
            var issuer = _configuration["Jwt:Issuer"] ?? "ShopApiServer";
            var audience = _configuration["Jwt:Audience"] ?? "ShopApiClient";
            var expiryMinutes = double.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 60;

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var expiration = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var roleName = user.Role?.RoleName ?? "User";

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.IdUsers.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.IdUsers.ToString()),
                new Claim(ClaimTypes.Name, user.UserLogin),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("role", roleName),
                new Claim("isActive", user.IsActive.ToString().ToLowerInvariant()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiration,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiration);
        }
    }
}

