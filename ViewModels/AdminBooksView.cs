using System.Collections.Generic;
using idefny.Models;

namespace idefny.ViewModels
{
    public class AdminBooksView
    {
        public List<BorrowedBook> BorrowedBooks { get; set; }
        public List<Waitlist> Waitlist { get; set; }
        public List<UserBook> PurchasedBooks { get; set; }
    }
}
