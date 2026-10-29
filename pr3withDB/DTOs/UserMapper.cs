using BCrypt.Net;
using pr3withDB.Models;

namespace pr3withDB.DTOs;

/// <summary>
/// Статический класс-маппер для сущности User.
/// </summary>
public static class UserMapper
{
    public static UserDTO ToDto(User entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        return new UserDTO
        {
            Id = entity.IdUsers,
            UserLogin = entity.UserLogin,
            RoleId = entity.RoleId,
            IsActive = entity.IsActive,
            CreatedAt = entity.UserCreatedAt
        };
    }

    public static User ToEntity(CreateUserDTO dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        return new User
        {
            UserLogin = dto.UserLogin,
            UserPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = dto.RoleId,
            IsActive = dto.IsActive,
            UserCreatedAt = DateTime.UtcNow
        };
    }

    public static void ApplyUpdate(User entity, UpdateUserDTO dto)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        entity.UserLogin = dto.UserLogin;
        entity.UserPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        entity.RoleId = dto.RoleId;
        entity.IsActive = dto.IsActive;
    }

    public static void ApplyPatch(User entity, PatchUserDTO dto)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        if (!string.IsNullOrWhiteSpace(dto.UserLogin))
            entity.UserLogin = dto.UserLogin;

        if (!string.IsNullOrWhiteSpace(dto.Password))
            entity.UserPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        if (dto.RoleId.HasValue)
            entity.RoleId = dto.RoleId.Value;

        if (dto.IsActive.HasValue)
            entity.IsActive = dto.IsActive.Value;
    }
}
