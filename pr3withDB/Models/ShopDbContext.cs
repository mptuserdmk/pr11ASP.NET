using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace pr3withDB.Models;

public partial class ShopDbContext : DbContext
{
    public ShopDbContext()
    {
    }

    public ShopDbContext(DbContextOptions<ShopDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=DESKTOP-K0RJUHD\\SQLEXPRESS; Database=ShopDB; Integrated Security=True; TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.IdCarts).HasName("PK__Carts__AB8B6B27915524D8");

            entity.HasIndex(e => e.UserId, "UQ__Carts__B9BF3306B6F020DA").IsUnique();

            entity.Property(e => e.IdCarts).HasColumnName("ID_carts");
            entity.Property(e => e.CartCreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("cart_created_at");
            entity.Property(e => e.UserId).HasColumnName("user_ID");

            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .HasConstraintName("FK__Carts__user_ID__5CD6CB2B");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.IdCartItems).HasName("PK__CartItem__95DA062EEB2779F3");

            entity.Property(e => e.IdCartItems).HasColumnName("ID_cart_items");
            entity.Property(e => e.CartId).HasColumnName("cart_ID");
            entity.Property(e => e.CartItemQuantity).HasColumnName("cart_item_quantity");
            entity.Property(e => e.ProductId).HasColumnName("product_ID");
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(450);

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("FK__CartItems__cart___60A75C0F")
                .IsRequired(false);

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__CartItems__produ__619B8048");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.IdCategories).HasName("PK__Categori__487EC2758ACB8982");

            entity.HasIndex(e => e.CategoryName, "UQ__Categori__5189E25509780A1A").IsUnique();

            entity.Property(e => e.IdCategories).HasColumnName("ID_categories");
            entity.Property(e => e.CategoryDescription)
                .HasMaxLength(500)
                .HasColumnName("category_description");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .HasColumnName("category_name");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.IdOrders).HasName("PK__Orders__D16B93907D33F832");

            entity.Property(e => e.IdOrders).HasColumnName("ID_orders");
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("order_date");
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Pending")
                .HasColumnName("order_status");
            entity.Property(e => e.OrderTotalAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("order_total_amount");
            entity.Property(e => e.UserId).HasColumnName("user_ID");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Orders__user_ID__68487DD7");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.IdOrderItems).HasName("PK__OrderIte__59381DCD2A8F60D1");

            entity.Property(e => e.IdOrderItems).HasColumnName("ID_order_items");
            entity.Property(e => e.OrderId).HasColumnName("order_ID");
            entity.Property(e => e.OrderItemPriceAtPurchase)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("order_item_price_at_purchase");
            entity.Property(e => e.OrderItemQuantity).HasColumnName("order_item_quantity");
            entity.Property(e => e.ProductId).HasColumnName("product_ID");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderItem__order__6D0D32F4");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__produ__6E01572D");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.IdProducts).HasName("PK__Products__13C16398A37A969E");

            entity.Property(e => e.IdProducts).HasColumnName("ID_products");
            entity.Property(e => e.CategoryId).HasColumnName("category_ID");
            entity.Property(e => e.ProductDescription).HasColumnName("product_description");
            entity.Property(e => e.ProductName)
                .HasMaxLength(200)
                .HasColumnName("product_name");
            entity.Property(e => e.ProductPrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("product_price");
            entity.Property(e => e.ProductStockQuantity).HasColumnName("product_stock_quantity");
            entity.Property(e => e.ProductImageUrl)
                .HasMaxLength(500)
                .HasColumnName("product_image_url");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__catego__5812160E");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.IdRoles).HasName("PK__Roles__8889B1720F08585B");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__783254B1CA30A079").IsUnique();

            entity.Property(e => e.IdRoles).HasColumnName("ID_roles");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdUsers).HasName("PK__Users__18069104AD80DEB1");

            entity.HasIndex(e => e.UserLogin, "UQ__Users__9EA1B5AF6BF51941").IsUnique();

            entity.Property(e => e.IdUsers).HasColumnName("ID_users");
            entity.Property(e => e.RoleId).HasColumnName("role_ID");
            entity.Property(e => e.UserCreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("user_created_at");
            entity.Property(e => e.UserLogin)
                .HasMaxLength(50)
                .HasColumnName("user_login");
            entity.Property(e => e.UserPasswordHash)
                .HasColumnName("user_password_hash");
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Users__role_ID__4F7CD00D");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
