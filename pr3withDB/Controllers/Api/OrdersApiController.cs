using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using pr3withDB.Common;
using pr3withDB.DTOs;
using pr3withDB.Models;
using pr3withDB.Repositories;
using pr3withDB.Services;

namespace pr3withDB.Controllers.Api;

/// <summary>
/// API контроллер для оформления и управления заказами с защитой JWT и Resource-based policy.
/// </summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "RequireActiveUser")]
public class OrdersApiController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IOrderRepository _orderRepository;
    private readonly IAuthorizationService _authorizationService;

    public OrdersApiController(
        IOrderService orderService,
        IOrderRepository orderRepository,
        IAuthorizationService authorizationService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    /// <summary>
    /// Получение списка заказов с пагинацией и возвратом общего количества в X-Total-Count.
    /// Пользователи с ролью User видят только свои заказы; Администратор видит любые заказы.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OrderReadDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? userId = null)
    {
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (int.TryParse(currentUserIdStr, out var currentUserId))
            {
                userId = currentUserId; // Принудительно ограничиваем обычного пользователя только своими заказами
            }
        }

        var result = await _orderService.GetPagedOrdersAsync(page, pageSize, userId);

        Response.Headers.Append("X-Total-Count", result.Data!.TotalCount.ToString());
        Response.Headers.Append("Access-Control-Expose-Headers", "X-Total-Count");

        return Ok(result.Data.Items);
    }

    /// <summary>
    /// Получение конкретного заказа по идентификатору с Resource-based проверкой прав.
    /// Обычный пользователь может получить ТОЛЬКО свой заказ; Администратор — любой.
    /// При попытке запроса чужого заказа возвращается 403 Forbidden.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderReadDto>> GetById(int id)
    {
        var orderEntity = await _orderRepository.GetByIdAsync(id);
        if (orderEntity == null)
        {
            return NotFound(new { message = $"Заказ с ID {id} не найден." });
        }

        // Проверка Resource-Based Policy через кастомный AuthorizationHandler
        var authResult = await _authorizationService.AuthorizeAsync(User, orderEntity, "OwnerOrAdminPolicy");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var result = await _orderService.GetOrderByIdAsync(id);
        return Ok(result.Data);
    }

    /// <summary>
    /// Оформление нового заказа.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderReadDto>> Create([FromBody] CreateOrderDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Если не администратор, принудительно выставляем UserId текущего аутентифицированного пользователя
        if (!User.IsInRole("Admin"))
        {
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (int.TryParse(currentUserIdStr, out var currentUserId))
            {
                dto.UserId = currentUserId;
            }
        }

        var result = await _orderService.CreateOrderAsync(dto);
        if (!result.IsSuccess)
        {
            if (result.ErrorType == ServiceErrorType.NotFound)
            {
                return NotFound(new { message = result.ErrorMessage });
            }
            return BadRequest(new { message = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Отмена заказа с Resource-based проверкой прав.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(OrderReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderReadDto>> Cancel(int id)
    {
        var orderEntity = await _orderRepository.GetByIdAsync(id);
        if (orderEntity == null)
        {
            return NotFound(new { message = $"Заказ с ID {id} не найден." });
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, orderEntity, "OwnerOrAdminPolicy");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var result = await _orderService.CancelOrderAsync(id);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Удаление заказа — защищено Role-based policy (только роль Admin).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _orderService.DeleteOrderAsync(id);
        if (!result.IsSuccess)
        {
            return NotFound(new { message = result.ErrorMessage });
        }

        return NoContent();
    }
}
