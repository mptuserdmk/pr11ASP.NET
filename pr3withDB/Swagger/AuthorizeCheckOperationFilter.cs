using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace pr3withDB.Swagger
{
    /// <summary>
    /// Фильтр Swagger, который добавляет требование Bearer-авторизации и иконку замка
    /// ТОЛЬКО к тем методам/контроллерам, которые помечены атрибутом [Authorize] 
    /// и не имеют [AllowAnonymous].
    /// </summary>
    public class AuthorizeCheckOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var methodAuth = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
            var classAuth = context.MethodInfo.DeclaringType != null && 
                            context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

            var methodAnon = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();
            var classAnon = context.MethodInfo.DeclaringType != null && 
                            context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

            // Если метод явно разрешает анонимный доступ - замок не нужен
            if (methodAnon) return;

            // Если есть [Authorize] на методе или классе (и нет [AllowAnonymous])
            if (methodAuth || (classAuth && !classAnon))
            {
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized — требуется валидный JWT-токен" });
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden — недостаточно прав или заблокирован" });

                var jwtSecuritySchemeRef = new OpenApiSecuritySchemeReference("Bearer");

                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        [jwtSecuritySchemeRef] = new List<string>()
                    }
                };
            }
        }
    }
}
