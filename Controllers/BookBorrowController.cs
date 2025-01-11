using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using idefny.Models;
using System.Linq;
using System.Threading.Tasks;
using idefny.Data;
using Microsoft.AspNetCore.Identity;

namespace idefny.Controllers
{
    public class BookBorrowController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartController> _logger;
        private readonly UserManager<Users> _userManager;

        public BookBorrowController(AppDbContext context, ILogger<CartController> logger, UserManager<Users> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyBorrowedBooks()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            // Get the logged-in user's ID

            // Fetch the borrowed books for the current user
            var borrowedBooks = await _context.BorrowedBooks
                .Where(b => b.UserId == currentUserId) // Filter by the current user's ID
                .Include(b => b.Book) // Include book details (if needed)
                .ToListAsync();

            // Return the borrowed books to the view
            return View("MyBorrowedBooks", borrowedBooks); // Specify the correct view
        }
        public async Task<IActionResult> UserOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = user.Id;

            // جلب جميع الطلبات الخاصة بالمستخدم مع تفاصيل العناصر
            var orders = await _context.Orders
                                       .Include(o => o.OrderItems)
                                       .ThenInclude(oi => oi.Book)
                                       .Where(o => o.UserId == userId)
                                       .OrderByDescending(o => o.OrderDate)
                                       .ToListAsync();
            return View("UserOrders", orders);
        }

        public async Task<IActionResult> MyWaitlist()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            // Get the logged-in user's ID

            // Fetch the borrowed books for the current user
            var waitlist = await _context.Waitlists
                .Where(b => b.UserId == currentUserId) // Filter by the current user's ID
                .Include(b => b.Book) // Include book details (if needed)
                .ToListAsync();

            // Return the borrowed books to the view
            return View("MyWaitlist", waitlist); // Specify the correct view
        }
        public async Task<IActionResult> MyPurchasedBooks()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = user.Id;

            // Fetch purchased books from UserBooks
            var purchasedBooks = await _context.UserBooks
                                                .Where(ub => ub.UserId == userId)
                                                .Include(ub => ub.Book)  // Include related Book details
                                                .ToListAsync();

            if (purchasedBooks == null || !purchasedBooks.Any())
            {
                ViewBag.Message = "You have not purchased any books yet.";
            }

            return View(purchasedBooks);  // Pass purchasedBooks list to view
        }
        [HttpPost]
        public async Task<IActionResult> ReturnBook(int borrowedBookId)
        {
            var borrowedBook = await _context.BorrowedBooks
                                             .Include(b => b.Book)
                                             .FirstOrDefaultAsync(b => b.Id == borrowedBookId);

            if (borrowedBook == null)
            {
                return NotFound(); // Handle case where the borrowed book is not found
            }

            // Increase the book's stock if applicable
            borrowedBook.Book.AvailableForBorrow++;

            // Remove the borrowed book entry
            _context.BorrowedBooks.Remove(borrowedBook);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyBorrowedBooks"); // Redirect to the list of borrowed books
        }
        public async Task<IActionResult> Removewaitlist(int borrowedBookId)
        {
            var waitlist = await _context.Waitlists
                                            .Include(b => b.Book)
                                            .FirstOrDefaultAsync(b => b.Id == borrowedBookId);

            if (waitlist == null)
            {
                return NotFound(); // Handle case where the borrowed book is not found
            }

            // Increase the book's stock if applicable
            waitlist.Book.NumberOfCopies++;

            // Remove the borrowed book entry
            _context.Waitlists.Remove(waitlist);
            await _context.SaveChangesAsync();

            return RedirectToAction("Mywaitlist"); // Redirect to the list of borrowed books

        }

        public async Task<IActionResult> Removebook(int borrowedBookId)
        {
            var book = await _context.UserBooks
                                            .Include(b => b.Book)
                                            .FirstOrDefaultAsync(b => b.Id == borrowedBookId);

            if (book == null)
            {
                return NotFound(); // Handle case where the borrowed book is not found
            }

            // Increase the book's stock if applicable
            book.Book.NumberOfCopies++;

            // Remove the borrowed book entry
            _context.UserBooks.Remove(book);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyPurchedBooks"); // Redirect to the list of borrowed books

        }
        public async Task<IActionResult> AllOrders()
        {
            // جلب جميع الطلبات مع تفاصيل العناصر والكتب
            var orders = await _context.Orders
                                       .Include(o => o.OrderItems)
                                           .ThenInclude(oi => oi.Book) // جلب الكتاب المرتبط بكل OrderItem
                                       .OrderByDescending(o => o.OrderDate)
                                       .ToListAsync();

            // جلب بيانات المستخدم لكل طلب
            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var users = await _context.Users
                                      .Where(u => userIds.Contains(u.Id))
                                      .ToDictionaryAsync(u => u.Id, u => u.UserName);

            // إضافة اسم المستخدم إلى كل طلب
            foreach (var order in orders)
            {
                order.UserId = users.ContainsKey(order.UserId) ? users[order.UserId] : "Unknown User";
            }

            return View(orders);
        }


        [HttpGet]
        public IActionResult DownloadAsF2b(string pdfUrl)
        {
            // تأكد من وجود ملف PDF
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", pdfUrl.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found.");
            }

            // تحويل PDF إلى F2B (في الوقت الحالي، مجرد تغيير اسم الملف)
            var f2bFilePath = Path.ChangeExtension(filePath, ".f2b");
            System.IO.File.Copy(filePath, f2bFilePath, overwrite: true);

            // تقديم ملف F2B للتنزيل
            var fileBytes = System.IO.File.ReadAllBytes(f2bFilePath);
            var fileName = Path.GetFileName(f2bFilePath);

            // تنظيف ملف F2B المؤقت
            System.IO.File.Delete(f2bFilePath);

            return File(fileBytes, "application/octet-stream", fileName);
        }


    }
}
