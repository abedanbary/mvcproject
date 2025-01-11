namespace idefny.Models
{
    public class OrderItem
    {
        public int Id { get; set; } // معرف العنصر (Primary Key)
        public int OrderId { get; set; } // معرف الطلب (Foreign Key)
        public int BookId { get; set; } // معرف الكتاب
        public int Quantity { get; set; } // الكمية
        public decimal Price { get; set; } // السعر
        public bool IsBuying { get; set; }
        public Order Order { get; set; }
        public Book Book { get; set; } // العلاقة مع Book// Navigation property to Cart// هل تم الشراء أم التأجير
    }
}

