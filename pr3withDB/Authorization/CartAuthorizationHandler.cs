using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using pr3withDB.Models;

namespace pr3withDB.Authorization
{
    /// <summary>
    /// Обработчик ресурсной авторизации для корзины (Cart).
    /// </summary>
    public class CartAuthorizationHandler : AuthorizationHandler<SameOwnerOrAdminRequirement, Cart>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SameOwnerOrAdminRequirement requirement,
            Cart resource)
        {
            if (context.User == null || resource == null)
            {
                return Task.CompletedTask;
            }

            // Проверка активности
            var isActiveClaim = context.User.FindFirst("isActive")?.Value;
            if (!string.Equals(isActiveClaim, "true", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            // Администратору доступно всё
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

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

