using idefny.Data;
using idefny.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using idefny.Services;

public class ReviewController : Controller
{
    private readonly string senderEmail;
    private readonly string senderName;
    private readonly AppDbContext _context;
    private readonly UserManager<Users> _userManager;

    public ReviewController(AppDbContext context, UserManager<Users> userManager, IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        senderEmail = configuration["BrevoApi:SenderEmail"]!;
        senderName = configuration["BrevoApi:SenderName"]!;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitReview(int BookId, int Rating, string Comment)
    {
        var currentUserId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(currentUserId))
        {
            TempData["ErrorMessage"] = "You must be logged in to review a book.";
            return RedirectToAction("Details", "Store", new { id = BookId });
        }

        // Check if the user has purchased or borrowed the book
        var hasPurchasedBook = await _context.UserBooks
            .AnyAsync(ub => ub.UserId == currentUserId && ub.BookId == BookId);
        var hasBorrowedBook = await _context.BorrowedBooks
            .AnyAsync(bb => bb.UserId == currentUserId && bb.BookId == BookId);

        if (!hasPurchasedBook && !hasBorrowedBook)
        {
            TempData["ErrorMessage"] = "You must purchase or borrow the book before reviewing it.";
            return RedirectToAction("Details", "Store", new { id = BookId });
        }

        if (ModelState.IsValid)
        {
            // Create and save the review
            var review = new Review
            {
                BookId = BookId,
                UserId = currentUserId,
                Rating = Rating,
                Comment = Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Send confirmation email
         
            TempData["SuccessMessage"] = "Review submitted successfully!";
            return RedirectToAction("Details", "Store", new { id = BookId });
        }

        TempData["ErrorMessage"] = "Invalid form submission.";
        return RedirectToAction("Error");
    }
}

