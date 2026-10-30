using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pr3withDB.Models;

namespace pr3withDB.Controllers
{
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class OrdersController : Controller
    {
        private readonly ShopDbContext _context;

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public OrdersController(ShopDbContext context)
        {
            _context = context;
        }



        public async Task<IActionResult> CheckoutSuccess(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.IdOrders == orderId && o.UserId == GetUserId());
            if (order == null)
            {
                return RedirectToAction("Index", "Products");
            }

            return View(order);
        }
    }
}
