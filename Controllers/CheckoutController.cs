using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json; // For JSON parsing
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging; // For ILogger
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using idefny.Models;
using idefny.Data;

namespace idefny.Controllers
{
    public class CheckoutController : Controller
    {
        private string PaypalClientId { get; set; }
        private string PaypalSecret { get; set; }
        private string PaypalUrl { get; set; }
        private readonly ILogger<CheckoutController> _logger; // Add ILogger for logging
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;

        // Inject ILogger into constructor
        public CheckoutController(IConfiguration configuration, ILogger<CheckoutController> logger, UserManager<Users> userManager, AppDbContext context)
        {
            PaypalClientId = configuration["PaypalSettings:ClientId"];
            PaypalSecret = configuration["PaypalSettings:Secret"];
            PaypalUrl = configuration["PaypalSettings:Url"];
            _logger = logger; // Initialize the logger
            _userManager = userManager;
            _context = context;
        }

        // GET: /<controller>/
        public async Task<IActionResult> Index()
        {
            decimal price = await GetPrice();  // Get the price from the cart
            ViewBag.Price = price;             // Pass the price to the view
            ViewBag.PaypalClientId = PaypalClientId;  // Pass the PayPal client ID to the view
            return View();
        }


        [HttpPost]
        public async Task<JsonResult> CompleteOrder([FromBody] JsonObject data)
        {
            var orderId = data?["orderId"]?.ToString();
            if (orderId == null)
            {
                return new JsonResult("error");
            }

            string accessToken = await GetPaypalAccessToken();
            string url = PaypalUrl + "/v2/checkout/orders/" + orderId + "/capture";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent("", null, "application/json");
                var httpResponse = await client.SendAsync(requestMessage);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();
                    var jsonResponse = JsonNode.Parse(strResponse);
                    if (jsonResponse != null)
                    {
                        string paypalOrderStatus = jsonResponse["status"]?.ToString() ?? "";
                        if (paypalOrderStatus == "COMPLETED")
                        {
                            return new JsonResult("success");
                        }
                    }
                }
            }
            return new JsonResult("error");
        }

        public async Task<JsonResult> CreateOrder([FromBody] JsonObject data)
        {
            var totalAmount = data?["amount"]?.ToString();
            if (string.IsNullOrEmpty(totalAmount) || !decimal.TryParse(totalAmount, out _))
            {
                _logger.LogWarning("Invalid or missing amount in the request.");
                return new JsonResult(new { id = "", error = "Invalid or missing amount" });
            }

            JsonObject createOrderRequest = new JsonObject();
            createOrderRequest.Add("intent", "CAPTURE");

            JsonObject amount = new JsonObject();
            amount.Add("currency_code", "USD");
            amount.Add("value", totalAmount);

            JsonObject purchaseUnit1 = new JsonObject();
            purchaseUnit1.Add("amount", amount);

            JsonArray purchaseUnits = new JsonArray();
            purchaseUnits.Add(purchaseUnit1);
            createOrderRequest.Add("purchase_units", purchaseUnits);

            string accessToken = await GetPaypalAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to retrieve PayPal access token.");
                return new JsonResult(new { id = "", error = "Failed to retrieve PayPal access token" });
            }

            string url = $"{PaypalUrl}/v2/checkout/orders";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(createOrderRequest.ToString(), Encoding.UTF8, "application/json")
                };

                try
                {
                    var httpResponse = await client.SendAsync(requestMessage);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var strResponse = await httpResponse.Content.ReadAsStringAsync();
                        var jsonResponse = JsonNode.Parse(strResponse);

                        string paypalOrderId = jsonResponse?["id"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(paypalOrderId))
                        {
                            _logger.LogInformation("PayPal order created successfully with ID: " + paypalOrderId);
                            return new JsonResult(new { Id = paypalOrderId });
                        }
                    }
                    else
                    {
                        var errorResponse = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to create order. PayPal response: " + errorResponse);
                        return new JsonResult(new { id = "", error = "Failed to create order", details = errorResponse });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error during PayPal order creation: " + ex.Message);
                    return new JsonResult(new { id = "", error = "Error during request", details = ex.Message });
                }
            }

            return new JsonResult(new { id = "", error = "Unknown error occurred" });
        }

        public async Task<string> Token()
        {
            return await GetPaypalAccessToken();
        }

        private async Task<string> GetPaypalAccessToken()
        {
            string accessToken = "";
            string url = $"{PaypalUrl}/v1/oauth2/token";

            using (var client = new HttpClient())
            {
                var credentials64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PaypalClientId}:{PaypalSecret}"));
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials64}");

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
                };

                try
                {
                    var httpResponse = await client.SendAsync(requestMessage);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var strResponse = await httpResponse.Content.ReadAsStringAsync();
                        var jsonResponse = JsonDocument.Parse(strResponse);
                        accessToken = jsonResponse.RootElement.GetProperty("access_token").GetString() ?? "";
                    }
                    else
                    {
                        var errorResponse = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Error fetching access token: " + errorResponse);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error during access token request: " + ex.Message);
                }
            }

            return accessToken;
        }
        private async Task<decimal> GetPrice()
        {
            // الحصول على المستخدم الحالي
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                Console.WriteLine("المستخدم غير مسجل الدخول.");
                return 0m; // أو التعامل مع هذه الحالة بشكل مختلف
            }

            var userId = user.Id;
            Console.WriteLine($"جلب عربة التسوق للمستخدم ذو المعرف: {userId}");

            // جلب عربة التسوق مع العناصر والكتب المرتبطة
            var cart = await _context.Carts
                                     .Include(c => c.CartItems)
                                     .ThenInclude(ci => ci.Book)
                                     .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                Console.WriteLine("لم يتم العثور على عربة تسوق للمستخدم.");
                return 0m; // أو التعامل مع هذه الحالة بشكل مختلف
            }

            if (cart.CartItems == null || !cart.CartItems.Any())
            {
                Console.WriteLine("عربة التسوق فارغة.");
                return 0m; // أو التعامل مع هذه الحالة بشكل مختلف
            }

            // حساب المبلغ الإجمالي لعربة التسوق
            decimal totalAmount = 0;
            foreach (var cartItem in cart.CartItems)
            {
                if (cartItem.Book == null)
                {
                    Console.WriteLine("الكتاب غير موجود لعنصر في عربة التسوق.");
                    continue; // تخطي هذا العنصر
                }

                // تسجيل السعر الأصلي والسعر بعد الخصم
                Console.WriteLine($"الكتاب: {cartItem.Book.Name}");
                Console.WriteLine($"السعر الأصلي: {cartItem.Book.Price}");
                Console.WriteLine($"السعر بعد الخصم: {cartItem.Book.DiscountedPrice}");

                // استخدام السعر بعد الخصم بدلًا من السعر الأصلي
                decimal itemPrice = cartItem.Book.DiscountedPrice;

                if (cartItem.IsBuying2)
                {
                    // إذا كان الشراء كاملًا، نضيف السعر بعد الخصم
                    Console.WriteLine($"شراء العنصر كاملًا: {itemPrice}");
                    totalAmount += itemPrice;
                }
                else
                {
                    // إذا كان التأجير، نضيف 20% من السعر بعد الخصم
                    decimal rentalPrice = itemPrice * 0.2m;
                    Console.WriteLine($"تأجير العنصر: {rentalPrice} (20% من {itemPrice})");
                    totalAmount += rentalPrice;
                }
            }

            totalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            return totalAmount;
        }


        // معالجة عملية الشراء
        public async Task<IActionResult> Index2(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound("الكتاب غير موجود.");
            }

            // تمرير بيانات الكتاب إلى الواجهة
            ViewBag.Price = book.Price;
            ViewBag.BookId = book.Id;
            ViewBag.PaypalClientId = PaypalClientId;

            return View(); // إعادة الواجهة الجديدة
        }

    }
}



