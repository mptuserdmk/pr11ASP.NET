using System.Threading.Tasks;
using pr3withDB.Common;
using pr3withDB.DTOs;

namespace pr3withDB.Services
{
    public interface IAuthService
    {
        Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterDto dto);
        Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto);
    }
}

