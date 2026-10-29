using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pr3withDB.DTOs;
using pr3withDB.Models;

namespace pr3withDB.Controllers.Api;

[ApiController]
[Route("api/carts")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireActiveUser")]
public class CartsApiController : ControllerBase
{
    private readonly ShopDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAuthorizationService _authorizationService;

    public CartsApiController(
        ShopDbContext context, 
        IMapper mapper,
        IAuthorizationService authorizationService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    /// <summary>
    /// Получение списка корзин с пагинацией и сортировкой по Id.
    /// Обычный пользователь видит только свою корзину, Администратор — все.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CartDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<CartDTO>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Carts.AsNoTracking();

        if (!User.IsInRole("Admin"))
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (int.TryParse(userIdStr, out var currentUserId))
            {
                query = query.Where(c => c.UserId == currentUserId);
            }
        }

        var carts = await query
            .OrderBy(c => c.IdCarts)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = _mapper.Map<IEnumerable<CartDTO>>(carts);
        return Ok(result);
    }

    /// <summary>
    /// Получение корзины по идентификатору с Resource-based проверкой владельца.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CartDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartDTO>> GetById(int id)
    {
        var cart = await _context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCarts == id);

        if (cart == null)
        {
            return NotFound(new { message = $"Корзина с ID {id} не найдена." });
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, cart, "OwnerOrAdminPolicy");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        return Ok(_mapper.Map<CartDTO>(cart));
    }

    /// <summary>
    /// Создание новой корзины.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CartDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartDTO>> Create([FromBody] CreateCartDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!User.IsInRole("Admin"))
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (int.TryParse(userIdStr, out var currentUserId))
            {
                dto.UserId = currentUserId;
            }
        }

        var userExists = await _context.Users.AnyAsync(u => u.IdUsers == dto.UserId);
        if (!userExists)
        {
            ModelState.AddModelError(nameof(dto.UserId), $"Пользователь с ID {dto.UserId} не существует.");
            return BadRequest(ModelState);
        }

        var cartAlreadyExists = await _context.Carts.AnyAsync(c => c.UserId == dto.UserId);
        if (cartAlreadyExists)
        {
            ModelState.AddModelError(nameof(dto.UserId), $"Корзина для пользователя с ID {dto.UserId} уже существует.");
            return BadRequest(ModelState);
        }

        var cart = _mapper.Map<Cart>(dto);
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        var createdDto = _mapper.Map<CartDTO>(cart);
        return CreatedAtAction(nameof(GetById), new { id = cart.IdCarts }, createdDto);
    }

    /// <summary>
    /// Полное обновление корзины (PUT).
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCartDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cart = await _context.Carts.FindAsync(id);
        if (cart == null)
        {
            return NotFound(new { message = $"Корзина с ID {id} не найдена." });
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, cart, "OwnerOrAdminPolicy");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        _mapper.Map(dto, cart);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Частичное обновление корзины (PATCH).
    /// </summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(int id, [FromBody] PatchCartDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cart = await _context.Carts.FindAsync(id);
        if (cart == null)
        {
            return NotFound(new { message = $"Корзина с ID {id} не найдена." });
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, cart, "OwnerOrAdminPolicy");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        _mapper.Map(dto, cart);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Удаление корзины по идентификатору.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var cart = await _context.Carts.FindAsync(id);
        if (cart == null)
        {
            return NotFound(new { message = $"Корзина с ID {id} не найдена." });
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, cart, "OwnerOrAdminPolicy");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        _context.Carts.Remove(cart);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
