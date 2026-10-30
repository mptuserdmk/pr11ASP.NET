using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pr3withDB.Models;
using pr3withDB.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace pr3withDB.Controllers
{
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class CartController : Controller
    {
        private readonly ShopDbContext _context;

        public CartController(ShopDbContext context)
        {
            _context = context;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // 1. Index: secure retrieval of cart items for the logged-in user
        public async Task<IActionResult> Index()
        {
            string userId = GetUserId();

            var items = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            var viewModel = new CartViewModel
            {
                Items = items.Select(ci => new CartItemViewModel
                {
                    Id = ci.IdCartItems,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.ProductName,
                    ProductPrice = ci.Product.ProductPrice,
                    Quantity = ci.CartItemQuantity,
                    ImagePath = ci.Product.ProductImageUrl
                }).ToList()
            };

            return View(viewModel);
        }

        // 2. AddToCart: safely add a product to the user's cart
        public async Task<IActionResult> AddToCart(int productId)
        {
            string userId = GetUserId();

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound("Товар не найден");
            }

            var item = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);

            if (item == null)
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = productId,
                    CartItemQuantity = 1
                });
            }
            else
            {
                item.CartItemQuantity++;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // 3. RemoveFromCart: safely remove a cart item (with ownership check)
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            string userId = GetUserId();

            var item = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.IdCartItems == id && ci.UserId == userId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // 4. UpdateQuantity: safely change quantity of a cart item (with ownership check)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int id, int quantity)
        {
            string userId = GetUserId();

            if (quantity <= 0)
            {
                return RedirectToAction("RemoveFromCart", new { id = id });
            }

            var item = await _context.CartItems
                .FirstOrDefaultAsync(ci => ci.IdCartItems == id && ci.UserId == userId);

            if (item != null)
            {
                item.CartItemQuantity = quantity;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // 5. Checkout: transactional checkout that creates an order and clears the cart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            string userId = GetUserId();
            int userIdInt = int.Parse(userId);

            // Fetch cart items for transaction
            var cartItems = await _context.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == userId)
                .ToListAsync();

            if (cartItems == null || !cartItems.Any())
            {
                TempData["Error"] = "Ваша корзина пуста. Невозможно оформить заказ.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                decimal totalAmount = cartItems.Sum(ci => ci.Product.ProductPrice * ci.CartItemQuantity);

                // Create Order
                var order = new Order
                {
                    UserId = userIdInt,
                    OrderDate = DateTime.Now,
                    OrderTotalAmount = totalAmount,
                    OrderStatus = "Pending"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Create Order Items
                var orderItems = cartItems.Select(ci => new OrderItem
                {
                    OrderId = order.IdOrders,
                    ProductId = ci.ProductId,
                    OrderItemQuantity = ci.CartItemQuantity,
                    OrderItemPriceAtPurchase = ci.Product.ProductPrice
                }).ToList();

                _context.OrderItems.AddRange(orderItems);

                // Clear the Cart Items securely
                _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Redirect to success in Orders controller
                return RedirectToAction("CheckoutSuccess", "Orders", new { orderId = order.IdOrders });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Произошла непредвиденная ошибка при оформлении заказа: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}