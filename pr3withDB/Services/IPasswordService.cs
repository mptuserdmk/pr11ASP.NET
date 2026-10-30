using pr3withDB.Models;

namespace pr3withDB.Services
{
    public interface IPasswordService
    {
        string HashPassword(User user, string password);
        bool VerifyPassword(User user, string password, out bool needsRehash);
    }
}

