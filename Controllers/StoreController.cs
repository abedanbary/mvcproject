﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using idefny.Data;
using idefny.Models;
using System.Linq;
using System.Threading.Tasks;
using idefny.ViewModels;
using System.Net;
using System.Text.Json.Nodes;

public class StoreController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<Users> _userManager;

    public StoreController(AppDbContext context, UserManager<Users> userManager)
    {
        _context = context;
        _userManager = userManager;
    }



    public async Task<IActionResult> Details(int id)
    {
        // Retrieve the book from the database
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound();
        }

        // Get all reviews for the book
        var reviews = await _context.Reviews
            .Where(r => r.BookId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Calculate the average rating
        int totalReviews = reviews.Count;
        double averageRating = totalReviews > 0 ? reviews.Sum(r => r.Rating) / (double)totalReviews : 0;

        // Populate the view model
        var viewModel = new BookDetailsViewModel
        {
            Book = book,
            Reviews = reviews,
            NewReview = new Review(), // For submitting a new review
            raebook = Math.Round(averageRating) // Average rating rounded to the nearest integer
        };

        return View(viewModel);
    }










    public async Task<IActionResult> Index(string searchTerm, int? minAge, string categoryFilter, string sortBy)
    {
        // Get the books from the database
        var booksQuery = _context.Books.AsQueryable();

        // Apply search filter if searchTerm is provided
        if (!string.IsNullOrEmpty(searchTerm))
        {
            booksQuery = booksQuery.Where(b => b.Name.Contains(searchTerm) || b.Author.Contains(searchTerm));
        }

        // Apply age filter if minAge is provided
        if (minAge.HasValue)
        {
            booksQuery = booksQuery.Where(b => b.Age <= minAge);
        }

        // Apply category filter if categoryFilter is provided
        if (!string.IsNullOrEmpty(categoryFilter))
        {
            booksQuery = booksQuery.Where(b => b.Genre.ToLower() == categoryFilter.ToLower());
        }

        // Apply sorting by price
        if (!string.IsNullOrEmpty(sortBy))
        {
            booksQuery = sortBy.ToLower() == "price_low_to_high"
                ? booksQuery.OrderBy(b => b.Price)
                : booksQuery.OrderByDescending(b => b.Price);
        }
        else
        {
            booksQuery = booksQuery.OrderBy(b => b.Name); // Default sorting by name
        }

        // Execute the query and get the results
        var bookViewModels = await booksQuery
            .Select(book => new BookViewModel
            {
                Id = book.Id,
                Name = book.Name,
                Author = book.Author,
                Age = book.Age,
                Genre = book.Genre,
                NumberOfCopies = book.NumberOfCopies,
                DatePublished = book.DatePublished,
                Price = book.Price,
                IsAvailableForBorrow = book.IsAvailableForBorrow,
                AvailableForBorrow = book.AvailableForBorrow,
                ImageUrl = book.ImageUrl,
                DiscountPercentage2 = book.DiscountPercentage2,
                DiscountStartDate = book.DiscountStartDate,
                DiscountEndDate = book.DiscountEndDate,
                DiscountedPrice = book.DiscountedPrice
            })
            .ToListAsync();

        return View(bookViewModels);
    }


    /*  [HttpPost]
      public async Task<IActionResult> Waitlist(int id)
      {
          var book = await _context.Books.FindAsync(id);

          if (book == null)
          {
              return RedirectToAction(nameof(Index));

          }

          var currentUserId = _userManager.GetUserId(User);

          var existingWaitlistEntry = await _context.Waitlists
              .FirstOrDefaultAsync(w => w.BookId == id && w.UserId == currentUserId);

          if (existingWaitlistEntry != null)
          {
              ViewBag.Message = "You are already on the waitlist for this book.";
              return RedirectToAction(nameof(Index));
          }

          var borrowedBooks = await _context.BorrowedBooks
              .Where(b => b.BookId == id && b.ReturnDate2 > DateTime.UtcNow)
              .OrderBy(b => b.ReturnDate2)
              .ToListAsync();

          int waitTimeInDays = 0;
          var hasBorrowedBook = await _context.BorrowedBooks
                                                 .AnyAsync(b => b.BookId == id&& b.UserId == currentUserId);
          if (hasBorrowedBook)
          {
              return Json(new { success = false, message = "You already have this book borrowed." });
          }
          var borrow = await _context.BorrowedBooks.Where(b => b.BookId == id).OrderBy(b => b.ReturnDate2).ToListAsync();

          if (borrowedBooks.Any())
          {
              var firstReturnDate = borrowedBooks.First().ReturnDate2;
              waitTimeInDays = (firstReturnDate - DateTime.UtcNow).Days;

              var waitlist = new Waitlist
              {
                  BookId = book.Id,
                  UserId = currentUserId,
                  DateJoined = DateTime.UtcNow,
                  Dateborrowbook=
              };

              _context.Add(waitlist);
              await _context.SaveChangesAsync();

              ViewBag.Message = $"You have been added to the waitlist. The estimated wait time is {waitTimeInDays} days.";
          }

          return RedirectToAction(nameof(Index));
      }*/
    [HttpPost]
    public async Task<IActionResult> Waitlist(int id)
    {
        var book = await _context.Books.FindAsync(id);

        if (book == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);

        // Check if the user is already on the waitlist
        var existingWaitlistEntry = await _context.Waitlists
            .FirstOrDefaultAsync(w => w.BookId == id && w.UserId == currentUserId);

        if (existingWaitlistEntry != null)
        {
            ViewBag.Message = "You are already on the waitlist for this book.";
            return RedirectToAction(nameof(Index));
        }

        // Check if the user has already borrowed this book
        var hasBorrowedBook = await _context.BorrowedBooks
            .AnyAsync(b => b.BookId == id && b.UserId == currentUserId);

        if (hasBorrowedBook)
        {
            return Json(new { success = false, message = "You already have this book borrowed." });
        }

        // Get the list of borrowed books sorted by return date
        var borrowedBooks = await _context.BorrowedBooks
            .Where(b => b.BookId == id && b.ReturnDate2 > DateTime.UtcNow)
            .OrderBy(b => b.ReturnDate2)
            .ToListAsync();

        // Get the last entry on the waitlist for this book
        var lastWaitlistEntry = await _context.Waitlists
            .Where(w => w.BookId == id)
            .OrderByDescending(w => w.Dateborrowbook)
            .FirstOrDefaultAsync();

        DateTime dateBorrowBook;

        if (lastWaitlistEntry != null)
        {
            dateBorrowBook = lastWaitlistEntry.Dateborrowbook ?? DateTime.UtcNow;
            dateBorrowBook = dateBorrowBook.AddDays(7);
        }
        else if (borrowedBooks.Any())
        {
            // If there are no people in the waitlist, set to the return date of the first borrowed book
            dateBorrowBook = borrowedBooks.First().ReturnDate2;
        }
        else
        {
            // If no borrowed books or waitlist entries, assume immediate availability
            dateBorrowBook = DateTime.UtcNow;
        }

        // Add the user to the waitlist
        var waitlist = new Waitlist
        {
            BookId = book.Id,
            UserId = currentUserId,
            DateJoined = DateTime.UtcNow,
            Dateborrowbook = dateBorrowBook
        };

        _context.Add(waitlist);
        await _context.SaveChangesAsync();

        ViewBag.Message = $"You have been added to the waitlist. The estimated wait time is {(dateBorrowBook - DateTime.UtcNow).Days} days.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<JsonResult> BuyBookNow([FromBody] JsonObject data)
    {
        var bookId = data?["bookId"]?.ToString();
        if (string.IsNullOrEmpty(bookId) || !int.TryParse(bookId, out int id))
        {
            return Json(new { success = false, message = "معرف الكتاب غير صالح." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, message = "المستخدم غير مسجل الدخول." });
        }

        var userId = user.Id;
        var book = await _context.Books.FindAsync(id);
        if (book == null)
        {
            return Json(new { success = false, message = "الكتاب غير موجود." });
        }

        // إنشاء طلب للكتاب الواحد
        var order = new Order
        {
            UserId = userId,
            TotalPrice = book.Price,
            OrderDate = DateTime.UtcNow,
            OrderItems = new List<OrderItem>
        {
            new OrderItem
            {
                BookId = id,
                Quantity = 1,
                Price = book.Price,
                IsBuying = true
            }
        }
        };
        var userBook = new UserBook
        {
            UserId = userId,
            BookId = id,
            PurchaseDate = DateTime.UtcNow
        };


        // تحديث قاعدة البيانات
        book.NumberOfCopies -= 1;
        _context.Orders.Add(order);
        _context.UserBooks.Add(userBook); 
    await _context.SaveChangesAsync();

        return Json(new { success = true, orderId = order.Id });
    }


}

// Other actions related to books...

