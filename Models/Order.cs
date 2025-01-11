using System;
using System.Collections.Generic;

namespace idefny.Models
{
    public class Order
    {
        public int Id { get; set; } // معرف الطلب (Primary Key)
        public string UserId { get; set; } // معرف المستخدم
        public decimal TotalPrice { get; set; } // السعر الإجمالي
        public DateTime OrderDate { get; set; } // تاريخ الطلب
        public List<OrderItem> OrderItems { get; set; } // العناصر المشتراة
    }
}

