



using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using idefny.Data;
using idefny.Models;
using idefny.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


public class AutoReturnService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private Timer _timer;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly string senderEmail;
    private readonly string senderName;

    public AutoReturnService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _cancellationTokenSource = new CancellationTokenSource();
        senderEmail = configuration["BrevoApi:SenderEmail"]!;
        senderName = configuration["BrevoApi:SenderName"]!;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Timer to execute every minute
        _timer = new Timer(async state => await ExecuteAsync(_cancellationTokenSource.Token), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    // Send reminders for books that need to be returned in 5 days
                    await NotifyUsersAboutUpcomingReturnAsync(dbContext, cancellationToken);

                    // Process books that should be returned
                    var booksToReturn = await dbContext.BorrowedBooks
                        .Include(b => b.Book)
                        .Where(b => b.ReturnDate2 <= DateTime.UtcNow)
                        .ToListAsync(cancellationToken);

                    foreach (var borrowedBook in booksToReturn)
                    {
                        // Update book availability
                        if (borrowedBook.Book != null)
                        {
                            borrowedBook.Book.AvailableForBorrow++;
                        }

                        // Handle waitlist
                        await HandleWaitlistAsync(dbContext, borrowedBook, cancellationToken);

                        // Remove the returned borrowed book
                        dbContext.BorrowedBooks.Remove(borrowedBook);
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    Console.WriteLine($"Error in AutoReturnService: {ex.Message}");
                }
            }
        }
    }

    private async Task NotifyUsersAboutUpcomingReturnAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            // Fetch borrowed books with 5 days remaining
            var upcomingReturns = await dbContext.BorrowedBooks
                .Include(b => b.User)
                .Include(b => b.Book)
                .Where(b => b.ReturnDate2 <= DateTime.UtcNow.AddDays(5) && b.ReturnDate2 > DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var borrowedBook in upcomingReturns)
            {
                if (borrowedBook.User != null && borrowedBook.Book != null)
                {
                    string subject = "Reminder: Return Book in 5 Days";
                    string message = $"Hello {borrowedBook.User.UserName},\n\n" +
                                     $"This is a friendly reminder that the book '{borrowedBook.Book.Name}' is due for return on {borrowedBook.ReturnDate2:dd/MM/yyyy}.\n" +
                                     $"Please ensure to return the book on time to avoid late fees.\n\nThank you for using our library!";

                    EmailSender.SendEmail(senderEmail, senderName, borrowedBook.User.Email, borrowedBook.User.UserName, subject, message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in NotifyUsersAboutUpcomingReturnAsync: {ex.Message}");
        }
    }

    private async Task HandleWaitlistAsync(AppDbContext dbContext, BorrowedBook borrowedBook, CancellationToken cancellationToken)
    {
        var waitlist = await dbContext.Waitlists
            .Where(w => w.BookId == borrowedBook.BookId)
            .OrderBy(w => w.DateJoined)
            .FirstOrDefaultAsync(cancellationToken);

        if (waitlist != null)
        {
            await AddBookToCartAsync(dbContext, waitlist, borrowedBook.Book, cancellationToken);
            dbContext.Waitlists.Remove(waitlist);
        }
    }

    private async Task AddBookToCartAsync(AppDbContext dbContext, Waitlist waitlist, Book book, CancellationToken cancellationToken)
    {
        var userCart = await dbContext.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == waitlist.UserId, cancellationToken)
            ?? new Cart { UserId = waitlist.UserId, CartItems = new List<CartItem>() };

        if (!dbContext.Carts.Contains(userCart))
        {
            dbContext.Carts.Add(userCart);
        }

        var cartItem = userCart.CartItems.FirstOrDefault(ci => ci.BookId == book.Id);

        if (cartItem == null)
        {
            userCart.CartItems.Add(new CartItem
            {
                BookId = book.Id,
                Quantity = 1,
                Price = book.DiscountedPrice,
                IsBuying2 = false,
                CartId = userCart.Id
            });

            var user = await dbContext.Users.FindAsync(waitlist.UserId);
            if (user != null)
            {
                string subject = "Book Added to Your Cart";
                string message = $"Hello {user.UserName},\n\nThe book '{book.Name}' has been added to your cart.\nYou can review and confirm your order.\n\nThank you!";

                EmailSender.SendEmail(senderEmail, senderName, user.Email, user.UserName, subject, message);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }
}








