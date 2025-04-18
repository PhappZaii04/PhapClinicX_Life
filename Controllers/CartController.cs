﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhapClinicX.Models;
using PhapClinicX.Models.Vnpay;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

namespace PhapClinicX.Controllers
{
    public class CartController : Controller
    {
        private readonly ClinicManagementContext _context;

        public CartController(ClinicManagementContext context)
        {
            _context = context;
        }

        public IActionResult Success(string message)
        {
            ViewBag.Message = message;
            return View();
        }


        public async Task<IActionResult> Index()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            await GetRelatedProducts(userId);

            decimal cartTotal = cartItems.Sum(item => (item.Product?.PriceSale ?? 0) * (item.Quantity ?? 0));
            ViewBag.ShippingFee = cartTotal >= 1000000 ? 0 : 30000;
            ViewBag.Discount = HttpContext.Session.GetInt32("DiscountAmount") ?? 0;

            return View(cartItems);
        }

        private async Task GetRelatedProducts(int? userId)
        {
            if (userId == null) return;

            var cartCategoryIds = await _context.Carts
                .Where(c => c.UserId == userId)
                .Join(_context.Products,
                      c => c.ProductId,
                      p => p.ProductId,
                      (c, p) => p.CategoryId)
                .Distinct()
                .ToListAsync();

            var cartProductIds = await _context.Carts
                .Where(c => c.UserId == userId)
                .Select(c => c.ProductId)
                .ToListAsync();

            var relatedProducts = await _context.Products
                .Where(p => cartCategoryIds.Contains(p.CategoryId) && !cartProductIds.Contains(p.ProductId))
                .OrderByDescending(p => p.PriceSale)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var existingCartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new Cart
                {
                    UserId = userId,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.Carts.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Cart");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartId, int quantity)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện thao tác này" });
            }

            var cartItem = await _context.Carts.FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId);
            if (cartItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ hàng" });
            }

            if (quantity <= 0)
            {
                _context.Carts.Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = quantity;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện thao tác này" });
            }

            var cartItem = await _context.Carts.FirstOrDefaultAsync(c => c.CartId == cartId && c.UserId == userId);
            if (cartItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ hàng" });
            }

            _context.Carts.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện thao tác này" });
            }

            var cartItems = _context.Carts.Where(c => c.UserId == userId);
            _context.Carts.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("DiscountAmount");
            HttpContext.Session.Remove("CouponCode");

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string code)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện thao tác này" });
            }

            if (string.IsNullOrEmpty(code))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá" });
            }

            var coupon = await _context.Discounts
                .FirstOrDefaultAsync(c => c.Code == code && c.IsActive && DateTime.Now >= c.StartDate && DateTime.Now <= c.EndDate);

            if (coupon == null)
            {
                return Json(new { success = false, message = "Mã giảm giá không hợp lệ hoặc đã hết hạn" });
            }

            HttpContext.Session.SetInt32("DiscountAmount", (int)coupon.DiscountPercent);
            HttpContext.Session.SetString("CouponCode", code);

            return Json(new { success = true, message = "Áp dụng mã giảm giá thành công" });
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // 🛍️ Lấy tên sản phẩm
            var productNames = await _context.Products
                .ToDictionaryAsync(p => p.ProductId, p => p.ProductName);
            ViewBag.ProductNames = productNames;

            var productImages = await _context.Products
    .ToDictionaryAsync(p => p.ProductId, p => p.Image);
            ViewBag.ProductImages = productImages;
            // 🛒 Lấy giỏ hàng
            var cartItems = await _context.Carts
                .Where(c => c.UserId == userId && c.IsCheckedOut == false)
                .Include(c => c.Product)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng trống.";
                return RedirectToAction("Index");
            }

            // 💵 Tính tiền
            var total = cartItems.Sum(c => (c.Quantity ?? 0) * (c.Product?.PriceSale ?? 0));
            decimal shippingFee = total >= 1000000 ? 0 : 30000;
            var finalTotal = total + shippingFee;

            // 📍 Lấy danh sách chi nhánh
            ViewBag.ListPhongKham = await _context.PhongKhams
                .Where(p => p.Isactive == true)
                .ToListAsync();

            // 💬 Lấy địa chỉ user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                ViewBag.UserAddress = user.Address; 
                ViewBag.UserPhone = user.Phone;
                ViewBag.UserName = user.FullName;
            }

            // 🚚 Truyền phụ trợ
            ViewBag.ShippingFee = shippingFee;
            ViewBag.ProductTotal = total;
            ViewBag.FinalTotal = finalTotal;

            // 📜 Hóa đơn tạm
            var invoice = new Invoice
            {
                UserId = userId,
                CreatedAt = DateTime.Now,
                Status = "Chờ thanh toán",
                TotalAmount = finalTotal,
                InvoiceType = "Product",
                InvoiceDetails = cartItems.Select(c => new InvoiceDetail
                {
                    ProductId = c.ProductId,
                    Quantity = c.Quantity,
                    Price = c.Product?.PriceSale // 👉 Sửa lỗi nhỏ nè
                }).ToList()
            };

            return View("InvoiceConfirmation", invoice);
        }

        public async Task<IActionResult> BankTransfer(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }


        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(string method, int phongKhamId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var cartItems = await _context.Carts
                .Where(c => c.UserId == userId && c.IsCheckedOut == false)
                .Include(c => c.Product)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Không có sản phẩm nào trong giỏ hàng.";
                return RedirectToAction("Index");
            }

            // Tính tiền
            decimal cartTotal = cartItems.Sum(item => (item.Product?.PriceSale ?? 0) * (item.Quantity ?? 0));
            decimal shippingFee = cartTotal >= 1_000_000 ? 0 : 30_000;
            decimal finalTotal = cartTotal + shippingFee;

            // Tạo hóa đơn
            var invoice = new Invoice
            {
                UserId = userId,
                CreatedAt = DateTime.Now,
                Status = "Chờ thanh toán", // Thay đổi status ban đầu thành "Chờ thanh toán"
                TotalAmount = finalTotal,
                PhongKhamId = phongKhamId,
                InvoiceType = "Sản phẩm",
                Method = method
            };

            await _context.Invoices.AddAsync(invoice);
            await _context.SaveChangesAsync();

            // Thêm chi tiết hóa đơn
            foreach (var item in cartItems)
            {
                var detail = new InvoiceDetail
                {
                    InvoiceId = invoice.InvoiceId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = finalTotal
                };
                await _context.InvoiceDetails.AddAsync(detail);
            }

            await _context.SaveChangesAsync();

            // Xóa giỏ hàng
            _context.Carts.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            // Nếu là chuyển khoản thì chuyển hướng đến trang hướng dẫn thanh toán ngân hàng
            if (method == "ChuyenKhoan")
            {
                // Cập nhật trạng thái cho phương thức chuyển khoản
                invoice.Status = "Chờ xác nhận thanh toán";
                await _context.SaveChangesAsync();

                return RedirectToAction("BankTransfer", new { id = invoice.InvoiceId });
            }

            if (method == "VNPAY")
            {
                // Ghi thanh toán với trạng thái "Đang xử lý"
                var payment = new Payment
                {
                    InvoiceId = invoice.InvoiceId,
                    UserId = userId,
                    Amount = finalTotal,
                    Method = method,
                    Status = "Đang xử lý",
                    CreatedAt = DateTime.Now
                };

                await _context.Payments.AddAsync(payment);
                await _context.SaveChangesAsync();

                // Gửi orderId là số nguyên, không có dấu #
                string orderId = invoice.InvoiceId.ToString();

                return RedirectToAction("CreatePaymentUrlVnpay", "Payment", new
                {
                    amount = finalTotal,
                    orderId = orderId, // Gửi orderId dạng số nguyên không có ký tự đặc biệt
                    description = $"Thanh toán đơn hàng {orderId} tại PhapClinicX"
                });
            }
            else // Các phương thức khác (tiền mặt, v.v.)
            {
                // Ghi thanh toán
                var payment = new Payment
                {
                    InvoiceId = invoice.InvoiceId,
                    UserId = userId,
                    Amount = finalTotal,
                    Method = method,
                    Status = "Đã thanh toán",
                    CreatedAt = DateTime.Now
                };

                await _context.Payments.AddAsync(payment);
                await _context.SaveChangesAsync();

                // Cập nhật trạng thái hóa đơn
                invoice.Status = "Đã thanh toán";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Success", "Cart");
        }



    }
}