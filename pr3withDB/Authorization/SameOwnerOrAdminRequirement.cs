using Microsoft.AspNetCore.Authorization;

namespace pr3withDB.Authorization
{
    /// <summary>
    /// Требование авторизации: доступ разрешен только владельцу ресурса либо пользователю с ролью Admin.
    /// </summary>
    public class SameOwnerOrAdminRequirement : IAuthorizationRequirement
    {
    }
}

