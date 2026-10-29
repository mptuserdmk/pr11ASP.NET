using System;
using System.ComponentModel.DataAnnotations;

namespace pr3withDB.DTOs;

/// <summary>
/// DTO для чтения данных пользователя (GET).
/// </summary>
public class UserReadDto
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public int RoleId { get; set; }
    public string? RoleName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO для чтения данных пользователя (GET) - обратная совместимость.
/// </summary>
public class UserDTO
{
    public int Id { get; set; }
    public string UserLogin { get; set; } = null!;
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO для создания пользователя (POST).
/// Не содержит Id и CreatedAt (формируются на сервере).
/// </summary>
public class CreateUserDTO
{
    [Required(ErrorMessage = "Логин обязателен.")]
    [StringLength(50, ErrorMessage = "Длина логина не должна превышать 50 символов.")]
    public string UserLogin { get; set; } = null!;

    [Required(ErrorMessage = "Пароль обязателен.")]
    [MinLength(6, ErrorMessage = "Минимальная длина пароля — 6 символов.")]
    public string Password { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный идентификатор роли.")]
    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO для полного обновления пользователя (PUT).
/// Все поля обязательны.
/// </summary>
public class UpdateUserDTO
{
    [Required(ErrorMessage = "Логин обязателен.")]
    [StringLength(50, ErrorMessage = "Длина логина не должна превышать 50 символов.")]
    public string UserLogin { get; set; } = null!;

    [Required(ErrorMessage = "Пароль обязателен.")]
    [MinLength(6, ErrorMessage = "Минимальная длина пароля — 6 символов.")]
    public string Password { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный идентификатор роли.")]
    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO для частичного обновления пользователя (PATCH).
/// Все поля nullable.
/// </summary>
public class PatchUserDTO
{
    [StringLength(50, ErrorMessage = "Длина логина не должна превышать 50 символов.")]
    public string? UserLogin { get; set; }

    [MinLength(6, ErrorMessage = "Минимальная длина пароля — 6 символов.")]
    public string? Password { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Укажите корректный идентификатор роли.")]
    public int? RoleId { get; set; }

    public bool? IsActive { get; set; }
}

public class UserCreateDto : CreateUserDTO { }
public class UserUpdateDto : UpdateUserDTO { }
