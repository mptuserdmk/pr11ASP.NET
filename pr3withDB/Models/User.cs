using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace pr3withDB.Models;

public partial class User
{
    public int IdUsers { get; set; }

    public string UserLogin { get; set; } = null!;

    public string UserPasswordHash { get; set; } = null!;

    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime UserCreatedAt { get; set; }

    public virtual Role? Role { get; set; }

    [NotMapped]
    public string? Password { get; set; }

    [ValidateNever]
    public virtual Cart? Cart { get; set; }

    [ValidateNever]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
