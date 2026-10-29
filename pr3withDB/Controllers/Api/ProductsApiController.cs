using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using pr3withDB.Common;
using pr3withDB.DTOs;
using pr3withDB.Services;

namespace pr3withDB.Controllers.Api;

/// <summary>
/// Тонкий API контроллер для управления товарами.
/// Чтение доступно публично, операции создания/модификации/удаления защищены Role-based policy (Admin).
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsApiController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsApiController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Получение списка товаров с пагинацией и возвратом общего количества в заголовке X-Total-Count (публичный доступ).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ProductReadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductReadDto>>> GetAll(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null)
    {
        var result = await _productService.GetPagedProductsAsync(page, pageSize, categoryId, search);

        // Добавляем заголовок X-Total-Count и выставляем Access-Control-Expose-Headers
        Response.Headers.Append("X-Total-Count", result.Data!.TotalCount.ToString());
        Response.Headers.Append("Access-Control-Expose-Headers", "X-Total-Count");

        return Ok(result.Data.Items);
    }

    /// <summary>
    /// Получение конкретного товара по идентификатору (публичный доступ).
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductReadDto>> GetById(int id)
    {
        var result = await _productService.GetProductByIdAsync(id);
        if (!result.IsSuccess)
        {
            return NotFound(new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Создание нового товара (Role-based policy: только Администратор).
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireAdminRole")]
    [ProducesResponseType(typeof(ProductReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductReadDto>> Create([FromBody] ProductCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _productService.CreateProductAsync(dto);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Полное обновление существующего товара (PUT) (Role-based policy: только Администратор).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _productService.UpdateProductAsync(id, dto);
        if (!result.IsSuccess)
        {
            if (result.ErrorType == ServiceErrorType.NotFound)
            {
                return NotFound(new { message = result.ErrorMessage });
            }
            return BadRequest(new { message = result.ErrorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// Частичное обновление полей товара (PATCH) (Role-based policy: только Администратор).
    /// </summary>
    [HttpPatch("{id:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(int id, [FromBody] PatchProductDTO dto)
    {
        var result = await _productService.PatchProductAsync(id, dto);
        if (!result.IsSuccess)
        {
            if (result.ErrorType == ServiceErrorType.NotFound)
            {
                return NotFound(new { message = result.ErrorMessage });
            }
            return BadRequest(new { message = result.ErrorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// Удаление товара по идентификатору (DELETE) (Role-based policy: только Администратор).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteProductAsync(id);
        if (!result.IsSuccess)
        {
            return NotFound(new { message = result.ErrorMessage });
        }

        return NoContent();
    }
}
