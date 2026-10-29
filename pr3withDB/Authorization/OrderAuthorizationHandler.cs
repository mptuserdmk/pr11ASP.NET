using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using pr3withDB.Models;

namespace pr3withDB.Authorization
{
    /// <summary>
    /// Обработчик ресурсной авторизации для заказов (Order).
    /// Разрешает доступ, если пользователь активен и является либо Администратором, либо владельцем заказа.
    /// </summary>
    public class OrderAuthorizationHandler : AuthorizationHandler<SameOwnerOrAdminRequirement, Order>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SameOwnerOrAdminRequirement requirement,
            Order resource)
        {
            if (context.User == null || resource == null)
            {
                return Task.CompletedTask;
            }

            // Проверка клейма активности пользователя
            var isActiveClaim = context.User.FindFirst("isActive")?.Value;
            if (!string.Equals(isActiveClaim, "true", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            // Администратору разрешен доступ к любому заказу
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Проверка принадлежности заказа текущему пользователю
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.User.FindFirst("sub")?.Value;

            if (int.TryParse(userIdClaim, out var currentUserId) && resource.UserId == currentUserId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

