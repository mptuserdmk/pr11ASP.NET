using System;
using pr3withDB.Models;

namespace pr3withDB.Services
{
    public interface IJwtTokenService
    {
        (string Token, DateTime Expiration) GenerateToken(User user);
    }
}

