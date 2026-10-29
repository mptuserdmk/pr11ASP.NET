using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pr3withDB.DTOs;
using pr3withDB.Models;

namespace pr3withDB.Controllers.Api;

[ApiController]
[Route("api/cart-items")]
[Produces("application/json")]
[AllowAnonymous]
public class CartItemsApiController : ControllerBase
{
    private readonly ShopDbContext _context;
    private readonly IMapper _mapper;

    public CartItemsApiController(ShopDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Получение списка позиций корзин с пагинацией и сортировкой по Id.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CartItemDTO>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CartItemDTO>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var cartItems = await _context.CartItems
            .AsNoTracking()
            .OrderBy(ci => ci.IdCartItems)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = _mapper.Map<IEnumerable<CartItemDTO>>(cartItems);
        return Ok(result);
    }

    /// <summary>
    /// Получение позиции корзины по идентификатору.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CartItemDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartItemDTO>> GetById(int id)
    {
        var cartItem = await _context.CartItems
            .AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.IdCartItems == id);

        if (cartItem == null)
        {
            return NotFound(new { message = $"Позиция корзины с ID {id} не найдена." });
        }

        return Ok(_mapper.Map<CartItemDTO>(cartItem));
    }

    /// <summary>
    /// Создание новой позиции корзины.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CartItemDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartItemDTO>> Create([FromBody] CreateCartItemDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.CartId.HasValue)
        {
            var cartExists = await _context.Carts.AnyAsync(c => c.IdCarts == dto.CartId.Value);
            if (!cartExists)
            {
                ModelState.AddModelError(nameof(dto.CartId), $"Корзина с ID {dto.CartId.Value} не существует.");
                return BadRequest(ModelState);
            }
        }

        var productExists = await _context.Products.AnyAsync(p => p.IdProducts == dto.ProductId);
        if (!productExists)
        {
            ModelState.AddModelError(nameof(dto.ProductId), $"Товар с ID {dto.ProductId} не существует.");
            return BadRequest(ModelState);
        }

        var cartItem = _mapper.Map<CartItem>(dto);
        _context.CartItems.Add(cartItem);
        await _context.SaveChangesAsync();

        var createdDto = _mapper.Map<CartItemDTO>(cartItem);
        return CreatedAtAction(nameof(GetById), new { id = cartItem.IdCartItems }, createdDto);
    }

    /// <summary>
    /// Полное обновление позиции корзины (PUT).
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCartItemDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cartItem = await _context.CartItems.FindAsync(id);
        if (cartItem == null)
        {
            return NotFound(new { message = $"Позиция корзины с ID {id} не найдена." });
        }

        if (dto.CartId.HasValue)
        {
            var cartExists = await _context.Carts.AnyAsync(c => c.IdCarts == dto.CartId.Value);
            if (!cartExists)
            {
                ModelState.AddModelError(nameof(dto.CartId), $"Корзина с ID {dto.CartId.Value} не существует.");
                return BadRequest(ModelState);
            }
        }

        var productExists = await _context.Products.AnyAsync(p => p.IdProducts == dto.ProductId);
        if (!productExists)
        {
            ModelState.AddModelError(nameof(dto.ProductId), $"Товар с ID {dto.ProductId} не существует.");
            return BadRequest(ModelState);
        }

        _mapper.Map(dto, cartItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Частичное обновление позиции корзины (PATCH).
    /// </summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(int id, [FromBody] PatchCartItemDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cartItem = await _context.CartItems.FindAsync(id);
        if (cartItem == null)
        {
            return NotFound(new { message = $"Позиция корзины с ID {id} не найдена." });
        }

        if (dto.CartId.HasValue)
        {
            var cartExists = await _context.Carts.AnyAsync(c => c.IdCarts == dto.CartId.Value);
            if (!cartExists)
            {
                ModelState.AddModelError(nameof(dto.CartId), $"Корзина с ID {dto.CartId.Value} не существует.");
                return BadRequest(ModelState);
            }
        }

        if (dto.ProductId.HasValue)
        {
            var productExists = await _context.Products.AnyAsync(p => p.IdProducts == dto.ProductId.Value);
            if (!productExists)
            {
                ModelState.AddModelError(nameof(dto.ProductId), $"Товар с ID {dto.ProductId.Value} не существует.");
                return BadRequest(ModelState);
            }
        }

        _mapper.Map(dto, cartItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Удаление позиции корзины по идентификатору.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var cartItem = await _context.CartItems.FindAsync(id);
        if (cartItem == null)
        {
            return NotFound(new { message = $"Позиция корзины с ID {id} не найдена." });
        }

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
