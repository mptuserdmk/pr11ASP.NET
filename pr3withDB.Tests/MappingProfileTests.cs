using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using pr3withDB.DTOs;
using pr3withDB.Mapping;
using pr3withDB.Models;
using Xunit;

namespace pr3withDB.Tests;

public class MappingProfileTests
{
    private readonly IMapper _mapper;

    public MappingProfileTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
        var serviceProvider = services.BuildServiceProvider();
        _mapper = serviceProvider.GetRequiredService<IMapper>();
    }

    [Fact]
    public void Cart_To_CartDTO_MapsCorrectly()
    {
        var cart = new Cart
        {
            IdCarts = 10,
            UserId = 42,
            CartCreatedAt = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc)
        };

        var dto = _mapper.Map<CartDTO>(cart);

        Assert.NotNull(dto);
        Assert.Equal(10, dto.Id);
        Assert.Equal(42, dto.UserId);
        Assert.Equal(cart.CartCreatedAt, dto.CreatedAt);
    }

    [Fact]
    public void CreateCartDTO_To_Cart_SetsCreatedAtAndUserId()
    {
        var dto = new CreateCartDTO { UserId = 5 };

        var cart = _mapper.Map<Cart>(dto);

        Assert.NotNull(cart);
        Assert.Equal(5, cart.UserId);
        Assert.True(cart.CartCreatedAt <= DateTime.UtcNow);
        Assert.True(cart.CartCreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void UpdateCartDTO_To_Cart_UpdatesUserId()
    {
        var cart = new Cart { IdCarts = 1, UserId = 10 };
        var dto = new UpdateCartDTO { UserId = 20 };

        _mapper.Map(dto, cart);

        Assert.Equal(1, cart.IdCarts);
        Assert.Equal(20, cart.UserId);
    }

    [Fact]
    public void PatchCartDTO_To_Cart_PartiallyUpdates()
    {
        var cart = new Cart { IdCarts = 2, UserId = 15 };
        var patchNull = new PatchCartDTO { UserId = null };
        _mapper.Map(patchNull, cart);
        Assert.Equal(15, cart.UserId);

        var patchValue = new PatchCartDTO { UserId = 99 };
        _mapper.Map(patchValue, cart);
        Assert.Equal(99, cart.UserId);
    }

    [Fact]
    public void CartItem_To_CartItemDTO_MapsCorrectly()
    {
        var item = new CartItem
        {
            IdCartItems = 7,
            CartId = 12,
            UserId = "user-abc-123",
            ProductId = 101,
            CartItemQuantity = 3
        };

        var dto = _mapper.Map<CartItemDTO>(item);

        Assert.NotNull(dto);
        Assert.Equal(7, dto.Id);
        Assert.Equal(12, dto.CartId);
        Assert.Equal("user-abc-123", dto.UserId);
        Assert.Equal(101, dto.ProductId);
        Assert.Equal(3, dto.CartItemQuantity);
    }

    [Fact]
    public void CreateCartItemDTO_To_CartItem_MapsCorrectly()
    {
        var dto = new CreateCartItemDTO
        {
            CartId = 5,
            UserId = "user-123",
            ProductId = 200,
            CartItemQuantity = 4
        };

        var item = _mapper.Map<CartItem>(dto);

        Assert.NotNull(item);
        Assert.Equal(5, item.CartId);
        Assert.Equal("user-123", item.UserId);
        Assert.Equal(200, item.ProductId);
        Assert.Equal(4, item.CartItemQuantity);
    }

    [Fact]
    public void PatchCartItemDTO_To_CartItem_UpdatesOnlyNonNullFields()
    {
        var item = new CartItem
        {
            IdCartItems = 1,
            CartId = 10,
            UserId = "initial-user",
            ProductId = 50,
            CartItemQuantity = 2
        };

        var patch = new PatchCartItemDTO
        {
            CartItemQuantity = 10
        };

        _mapper.Map(patch, item);

        Assert.Equal(10, item.CartId);
        Assert.Equal("initial-user", item.UserId);
        Assert.Equal(50, item.ProductId);
        Assert.Equal(10, item.CartItemQuantity);
    }

    [Fact]
    public void Product_To_ProductReadDto_FlattensCategoryName()
    {
        var product = new Product
        {
            IdProducts = 10,
            ProductName = "Смартфон",
            ProductPrice = 50000m,
            Category = new Category { IdCategories = 2, CategoryName = "Электроника" }
        };

        var dto = _mapper.Map<ProductReadDto>(product);

        Assert.NotNull(dto);
        Assert.Equal(10, dto.Id);
        Assert.Equal("Смартфон", dto.ProductName);
        Assert.Equal("Электроника", dto.CategoryName);
    }

    [Fact]
    public void Category_To_CategoryReadDto_ComputesProductsCount()
    {
        var category = new Category
        {
            IdCategories = 3,
            CategoryName = "Одежда",
            Products = new List<Product>
            {
                new() { IdProducts = 1, ProductName = "Футболка" },
                new() { IdProducts = 2, ProductName = "Джинсы" }
            }
        };

        var dto = _mapper.Map<CategoryReadDto>(category);

        Assert.NotNull(dto);
        Assert.Equal(3, dto.Id);
        Assert.Equal("Одежда", dto.CategoryName);
        Assert.Equal(2, dto.ProductsCount);
    }

    [Fact]
    public void Order_To_OrderReadDto_MapsNestedItems()
    {
        var order = new Order
        {
            IdOrders = 100,
            OrderDate = DateTime.UtcNow,
            User = new User { IdUsers = 1, UserLogin = "ivan" },
            OrderItems = new List<OrderItem>
            {
                new()
                {
                    IdOrderItems = 1,
                    OrderItemQuantity = 2,
                    OrderItemPriceAtPurchase = 1500m,
                    Product = new Product { IdProducts = 5, ProductName = "Мышь", ProductPrice = 1500m }
                }
            }
        };

        var dto = _mapper.Map<OrderReadDto>(order);

        Assert.NotNull(dto);
        Assert.Equal(100, dto.Id);
        Assert.Equal("ivan", dto.UserLogin);
        Assert.Single(dto.Items);
        Assert.Equal("Мышь", dto.Items[0].ProductName);
        Assert.Equal(2, dto.Items[0].OrderItemQuantity);
        Assert.Equal(3000m, dto.Items[0].Subtotal);
    }
}
