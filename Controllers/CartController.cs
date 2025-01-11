using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using idefny.Models;
using idefny.Data;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using idefny.Services;

namespace idefny.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartController> _logger;
        private readonly UserManager<Users> _userManager;
        private readonly string senderEmail;
        private readonly string senderName;

        public CartController(AppDbContext context, ILogger<CartController> logger, UserManager<Users> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
              senderEmail = configuration["BrevoApi:SenderEmail"]!;
            senderName = configuration["BrevoApi:SenderName"]!;

        }

        // GET: View Cart
        public async Task<IActionResult> ViewCart()
        {
            // Get the current authenticated user's ID
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");  // Redirect to login if user is not authenticated
            }

            var userId = user.Id;  // UserId to fetch cart

            // Check if cart exists for this user
            var cart = await _context.Carts.Include(c => c.CartItems).ThenInclude(ci => ci.Book)
                                            .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                // Optionally, handle this case if cart doesn't exist yet
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();  // Save new cart if not found
            }

            return View(cart);  // Pass the cart model to the view
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> AddToCart(int bookId, bool IsBuying2)
        {
            // Fetch the book from the database
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return Json(new { success = false, message = "Book not found." });
            }

            // Ensure the user is authenticated
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "You must be logged in to add items to the cart." });
            }

            var userId = user.Id;

            // Check if the user has already borrowed the book
            var hasBorrowedBook = await _context.BorrowedBooks
                                                .AnyAsync(b => b.BookId == bookId && b.UserId == userId);
            var hasBook = await _context.UserBooks
                                               .AnyAsync(b => b.BookId == bookId && b.UserId == userId);


            if (hasBorrowedBook && IsBuying2==false)
            {
                return Json(new { success = false, message = "You already have this book borrowed." });
            }

            var borrowedBooksCount = await _context.BorrowedBooks.CountAsync(b => b.UserId == userId );
            var waitlistCount = await _context.Waitlists .CountAsync(w => w.UserId == userId);

            if (borrowedBooksCount + waitlistCount >= 3&& IsBuying2==false)
            {
                return Json(new { success = false, message = "You cant boroow more than 3 books." });
            }


            // Check if the cart exists for the user
            var cart = await _context.Carts.Include(c => c.CartItems)
                                           .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                // Create a new cart if one doesn't exist
                cart = new Cart
                {
                    UserId = userId,
                    CartItems = new List<CartItem>()
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Check if the book is already in the cart
            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.BookId == bookId);
            if (cartItem == null)
            {
                // Add the book to the cart
                cartItem = new CartItem
                {
                    BookId = bookId,
                    Quantity = 1,  // Default quantity is 1
                    Price = book.DiscountedPrice,
                    CartId = cart.Id,
                    IsBuying2 = IsBuying2  // True for Buying, False for Borrowing
                };
               

                cart.CartItems.Add(cartItem);
            }
            else
            {
                // If the book is already in the cart, increase the quantity
                cartItem.Quantity++;
            }

            // Save changes to the database
            await _context.SaveChangesAsync();

            // Redirect to the ViewCart action
            return RedirectToAction("ViewCart", "Cart");
        }





        // POST: Update Item Quantity in Cart
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var cartItem = await _context.CartItems.FindAsync(cartItemId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Item not found in cart." });
            }

            // Update quantity
            cartItem.Quantity = quantity;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cart updated." });
        }

        // POST: Remove Item from Cart
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var cartItem = await _context.CartItems.FindAsync(cartItemId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Item not found in cart." });
            }

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Item removed from cart." });
        }


        [HttpPost]
        public async Task<IActionResult> CheckoutCart()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = user.Id;
            var cart = await _context.Carts
                                     .Include(c => c.CartItems)
                                     .ThenInclude(ci => ci.Book)
                                     .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return RedirectToAction("ViewCart", "Cart");
            }

            // حساب السعر الإجمالي
            decimal totalPrice = 0;
            foreach (var cartItem in cart.CartItems)
            {
                if (cartItem.IsBuying2)
                {
                    totalPrice += cartItem.Book.DiscountedPrice * cartItem.Quantity;
                }
                else
                {
                    totalPrice += (cartItem.Book.DiscountedPrice * 0.2m) * cartItem.Quantity;
                }
            }

            // إنشاء كائن Order
            var order = new Order
            {
                UserId = userId,
                TotalPrice = totalPrice,
                OrderDate = DateTime.UtcNow,
                OrderItems = new List<OrderItem>()
            };

            // فصل العناصر إلى شراء واستعارة
            var itemsToBuy = cart.CartItems.Where(ci => ci.IsBuying2).ToList();
            var itemsToBorrow = cart.CartItems.Where(ci => !ci.IsBuying2).ToList();

            // معالجة العناصر المشتراة
            foreach (var cartItem in itemsToBuy)
            {
                var userBook = new UserBook
                {
                    UserId = userId,
                    BookId = cartItem.BookId,
                    PurchaseDate = DateTime.UtcNow,
                };
                _context.UserBooks.Add(userBook);

                var book = await _context.Books.FindAsync(cartItem.BookId);
                if (book != null)
                {
                    book.NumberOfCopies -= cartItem.Quantity;
                }

                // إضافة العنصر إلى الطلب
                var orderItem = new OrderItem
                {
                    BookId = cartItem.BookId,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Book.Price,
                    IsBuying = true
                };
                order.OrderItems.Add(orderItem);
            }
           

            // معالجة العناصر المستعارة
            foreach (var cartItem in itemsToBorrow)
            {      var waitlist2 = await _context.Waitlists
                    .Where(w => w.BookId == cartItem.BookId)
                    .OrderBy(w => w.DateJoined)
                    .FirstOrDefaultAsync();
                var lastone = true;
                if (waitlist2 != null)
                {
                                       lastone = false;


                }


                // إنشاء سجل استعارة جديد للكتاب
                var borrowedBook = new BorrowedBook
                {
                    UserId = userId,
                    BookId = cartItem.BookId,
                    BorrowDate = DateTime.UtcNow,
                    DaysToBorrow = 14,
                   
                    ReturnDate2 = cartItem.Book.AvailableForBorrow == 1 && lastone==true ? DateTime.UtcNow.AddDays(5).AddMinutes(3): DateTime.UtcNow.AddMinutes(5).AddDays(3)
                };

                // إضافة الكتاب إلى سجلات الاستعارة
                _context.BorrowedBooks.Add(borrowedBook);

                // البحث عن أول شخص في قائمة الانتظار لهذا الكتاب
                var waitlist = await _context.Waitlists
                    .Where(w => w.BookId == cartItem.BookId)
                    .OrderBy(w => w.DateJoined)
                    .FirstOrDefaultAsync();

                // إذا كان هناك شخص في قائمة الانتظار
                if (waitlist != null)
                {
                    // تحديث تاريخ الاستعارة للمستخدم الأول في قائمة الانتظار
                    waitlist.Dateborrowbook = borrowedBook.ReturnDate2;

                    // حفظ التغييرات في قاعدة البيانات
                    _context.Waitlists.Update(waitlist);
                }

                // تحديث الكتاب المتاح للاستعارة (العدد المتاح يقل)
                var book = await _context.Books.FindAsync(cartItem.BookId);
                if (book != null)
                {
                    book.AvailableForBorrow--;
                }
           

            // إضافة العنصر إلى الطلب
            var orderItem = new OrderItem
                {
                    BookId = cartItem.BookId,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Book.Price * 0.2m,
                    IsBuying = false
                };
                order.OrderItems.Add(orderItem);
            }

            // حفظ الطلب في قاعدة البيانات
            _context.Orders.Add(order);

            // حذف العناصر المعالجة من عربة التسوق
            _context.CartItems.RemoveRange(itemsToBuy);
            _context.CartItems.RemoveRange(itemsToBorrow);

            // حفظ التغييرات في قاعدة البيانات
            await _context.SaveChangesAsync();
            string subject = "Order Confirmation";
            string message = $"Hello {user.UserName},\n\nYour order has been successfully placed!\n\nOrder Details:\n";
            foreach (var orderItem in order.OrderItems)
            {
                string itemType = orderItem.IsBuying ? "Purchase" : "Borrow";
                message += $"{itemType}: {orderItem.Quantity} x {orderItem.Book.Name} - Price: {orderItem.Price}\n";
            }
            message += $"\nTotal Price: {totalPrice}\n\nThank you for shopping with us!";

            // Send email
            EmailSender.SendEmail(senderEmail, senderName, user.Email, user.UserName, subject, message);



            // إرجاع استجابة ناجحة
            return Json(new { success = true, orderId = order.Id });
        }





    }
}

