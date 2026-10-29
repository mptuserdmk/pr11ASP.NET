using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pr3withDB.DTOs;
using pr3withDB.Models;

namespace pr3withDB.Controllers.Api;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireActiveUser")]
public class UsersApiController : ControllerBase
{
    private readonly ShopDbContext _context;

    public UsersApiController(ShopDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Получение списка пользователей с пагинацией (Role-based policy: только Администратор).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(IEnumerable<UserDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<UserDTO>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.IdUsers)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = users.Select(UserMapper.ToDto).ToList();
        return Ok(result);
    }

    /// <summary>
    /// Получение пользователя по идентификатору.
    /// Доступно администратору или самому пользователю.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDTO>> GetById(int id)
    {
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var isSelf = int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == id;

        if (!User.IsInRole("Admin") && !isSelf)
        {
            return Forbid();
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsers == id);

        if (user == null)
        {
            return NotFound(new { message = $"Пользователь с ID {id} не найден." });
        }

        return Ok(UserMapper.ToDto(user));
    }

    /// <summary>
    /// Создание нового пользователя через API администратора.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserDTO>> Create([FromBody] CreateUserDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var roleExists = await _context.Roles.AnyAsync(r => r.IdRoles == dto.RoleId);
        if (!roleExists)
        {
            ModelState.AddModelError(nameof(dto.RoleId), $"Роль с ID {dto.RoleId} не существует.");
            return BadRequest(ModelState);
        }

        var loginExists = await _context.Users.AnyAsync(u => u.UserLogin == dto.UserLogin);
        if (loginExists)
        {
            ModelState.AddModelError(nameof(dto.UserLogin), $"Пользователь с логином '{dto.UserLogin}' уже существует.");
            return BadRequest(ModelState);
        }

        var user = UserMapper.ToEntity(dto);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var createdDto = UserMapper.ToDto(user);
        return CreatedAtAction(nameof(GetById), new { id = user.IdUsers }, createdDto);
    }

    /// <summary>
    /// Полное обновление пользователя (PUT).
    /// Доступно администратору или самому пользователю.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var isSelf = int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == id;

        if (!User.IsInRole("Admin") && !isSelf)
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Пользователь с ID {id} не найден." });
        }

        var roleExists = await _context.Roles.AnyAsync(r => r.IdRoles == dto.RoleId);
        if (!roleExists)
        {
            ModelState.AddModelError(nameof(dto.RoleId), $"Роль с ID {dto.RoleId} не существует.");
            return BadRequest(ModelState);
        }

        var loginExists = await _context.Users.AnyAsync(u => u.UserLogin == dto.UserLogin && u.IdUsers != id);
        if (loginExists)
        {
            ModelState.AddModelError(nameof(dto.UserLogin), $"Пользователь с логином '{dto.UserLogin}' уже существует.");
            return BadRequest(ModelState);
        }

        UserMapper.ApplyUpdate(user, dto);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Частичное обновление пользователя (PATCH).
    /// Доступно администратору или самому пользователю.
    /// </summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(int id, [FromBody] PatchUserDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var isSelf = int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == id;

        if (!User.IsInRole("Admin") && !isSelf)
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Пользователь с ID {id} не найден." });
        }

        if (dto.RoleId.HasValue)
        {
            var roleExists = await _context.Roles.AnyAsync(r => r.IdRoles == dto.RoleId.Value);
            if (!roleExists)
            {
                ModelState.AddModelError(nameof(dto.RoleId), $"Роль с ID {dto.RoleId.Value} не существует.");
                return BadRequest(ModelState);
            }
        }

        if (dto.UserLogin != null)
        {
            var loginExists = await _context.Users.AnyAsync(u => u.UserLogin == dto.UserLogin && u.IdUsers != id);
            if (loginExists)
            {
                ModelState.AddModelError(nameof(dto.UserLogin), $"Пользователь с логином '{dto.UserLogin}' уже существует.");
                return BadRequest(ModelState);
            }
        }

        UserMapper.ApplyPatch(user, dto);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Удаление пользователя (Role-based policy: только Администратор).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Пользователь с ID {id} не найден." });
        }

        try
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "Невозможно удалить пользователя, так как с ним связаны заказы или корзина." });
        }

        return NoContent();
    }
}
