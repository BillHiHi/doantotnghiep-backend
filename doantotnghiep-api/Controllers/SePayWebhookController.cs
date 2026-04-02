using doantotnghiep_api.Data;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SePayWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<BookingHub> _hub;
        private readonly IEmailService _emailService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public SePayWebhookController(AppDbContext context, IHubContext<BookingHub> hub, IEmailService emailService, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _context = context;
            _hub = hub;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveWebhook([FromBody] SePayTransaction payload)
        {
            // ==========================================
            // 0. KIỂM TRA BẢO MẬT (XÁC THỰC WEBHOOK)
            // ==========================================
            var authHeader = Request.Headers["Authorization"].ToString();
            var configuredApiKey = _configuration["SePaySettings:ApiKey"];
            var expectedHeader = $"Apikey {configuredApiKey}";

            if (string.IsNullOrEmpty(authHeader) || !authHeader.Equals(expectedHeader, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[WEBHOOK] ❌ Cảnh báo: Sai API Key hoặc có người lạ gọi Webhook!");
                return Unauthorized(new { success = false, message = "Invalid API Key" });
            }

            if (payload == null)
            {
                return BadRequest("Invalid data");
            }

            // 1. Lấy nội dung chuyển khoản
            string content = payload.content;

            // 2. Tách lấy mã đơn hàng (RFxxxxxx)
            string paymentCode = ExtractPaymentCode(content);

            if (string.IsNullOrEmpty(paymentCode))
            {
                return Ok(new { success = false, message = "Payment code not found in content" });
            }

            // ==========================================
            // BƯỚC KIỂM TRA TRÙNG LẶP (IDEMPOTENCY)
            // Tránh tạo 2 đơn hàng nếu SePay lỡ gửi webhook 2 lần
            // ==========================================
            bool isAlreadyPaid = await _context.Bookings.AnyAsync(b => b.PaymentCode == paymentCode);
            if (isAlreadyPaid)
            {
                Console.WriteLine($"[WEBHOOK] ⚠️ Đơn hàng {paymentCode} đã được xử lý trước đó. Bỏ qua.");
                return Ok(new { success = true, message = "Order already processed" });
            }

            // 3. Tìm các ghế đang giữ với mã này
            var lockedSeats = await _context.SeatLocks
                .Where(x => x.PaymentCode == paymentCode && x.ExpiryTime > DateTime.UtcNow)
                .ToListAsync();

            if (!lockedSeats.Any())
            {
                Console.WriteLine($"[WEBHOOK] ❌ Nhận được tiền nhưng ghế cho mã {paymentCode} đã hết hạn giữ hoặc không tồn tại!");
                return Ok(new { success = false, message = "No pending seats found or expired" });
            }

            // 4. Kiểm tra số tiền khách chuyển so với tổng tiền đơn hàng
            decimal amountIn = payload.transferAmount;
            decimal expectedAmount = lockedSeats.First().TotalAmount ?? 0;

            if (amountIn < expectedAmount)
            {
                Console.WriteLine($"[WEBHOOK] ❌ Khách chuyển thiếu tiền. Yêu cầu: {expectedAmount}, Nhận: {amountIn}");
                // Có thể return Ok hoặc BadRequest tuỳ logic của bạn, return Ok để SePay không gửi lại nữa
                return Ok(new { success = false, message = "Insufficient amount" });
            }

            // 5. Chuyển từ SeatLock sang Bookings
            var now = DateTime.UtcNow;
            foreach (var lockItem in lockedSeats)
            {
                var booking = new Bookings
                {
                    UserId = lockItem.UserId,
                    ShowtimeId = lockItem.ShowtimeId,
                    SeatId = lockItem.SeatId,
                    BookingDate = now,
                    Status = "Paid",
                    TotalAmount = lockItem.TotalAmount ?? 0,
                    PaymentCode = lockItem.PaymentCode,
                    Combos = lockItem.Combos
                };
                _context.Bookings.Add(booking);
            }

            // GỬI EMAIL XÁC NHẬN (Chạy ngầm bằng Task.Run)
            var firstLock = lockedSeats.First();
            var seatIds = lockedSeats.Select(s => s.SeatId).ToList();

            _ = Task.Run(async () => {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                        Console.WriteLine($"[WEBHOOK] 📧 Bắt đầu chuẩn bị email cho User ID: {firstLock.UserId}");

                        var user = await scopedContext.Users.FindAsync(firstLock.UserId);
                        var showtime = await scopedContext.Showtimes
                            .Include(s => s.Movie)
                            .Include(s => s.Screen)
                                .ThenInclude(sc => sc.Theater)
                            .FirstOrDefaultAsync(s => s.ShowtimeId == firstLock.ShowtimeId);

                        if (user == null) Console.WriteLine("[WEBHOOK] ❌ Lỗi: Không tìm thấy thông tin User");
                        if (showtime == null) Console.WriteLine("[WEBHOOK] ❌ Lỗi: Không tìm thấy thông tin Suất chiếu");

                        if (user != null && !string.IsNullOrEmpty(user.Email) && showtime != null)
                        {
                            Console.WriteLine($"[WEBHOOK] 📤 Đang gửi mail tới: {user.Email}...");

                            var movie = showtime.Movie;
                            var posterUrl = movie?.PosterUrl;
                            var scopedConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                            var baseUrl = scopedConfig["AppBaseUrl"] ?? "http://localhost:5066";

                            if (!string.IsNullOrEmpty(posterUrl) && !posterUrl.StartsWith("http"))
                            {
                                posterUrl = baseUrl.TrimEnd('/') + "/" + posterUrl.Replace("wwwroot/", "").TrimStart('/');
                            }

                            var seats = await scopedContext.Seats.Where(s => seatIds.Contains(s.SeatId)).ToListAsync();
                            string seatNames = string.Join(", ", seats.Select(s => $"{s.RowNumber}{s.SeatNumber}"));
                            decimal totalAmountPaid = lockedSeats.Sum(s => s.TotalAmount ?? 0);

                            // Giải mã JSON combo bắp nước
                            string comboText = "";
                            if (!string.IsNullOrEmpty(firstLock.Combos))
                            {
                                try
                                {
                                    using (var doc = System.Text.Json.JsonDocument.Parse(firstLock.Combos))
                                    {
                                        var items = new List<string>();
                                        foreach (var item in doc.RootElement.EnumerateArray())
                                        {
                                            string name = item.GetProperty("name").GetString();
                                            int qty = item.GetProperty("qty").GetInt32();
                                            if (qty > 0) items.Add($"{qty}x {name}");
                                        }
                                        comboText = string.Join(", ", items);
                                    }
                                }
                                catch
                                {
                                    comboText = firstLock.Combos;
                                }
                            }

                            await scopedEmailService.SendTicketEmailAsync(
                                user.Email,
                                user.FullName ?? "Khách hàng",
                                user.PhoneNumber ?? "N/A",
                                movie?.Title ?? "Phim",
                                posterUrl ?? "",
                                showtime.Screen?.Theater?.Name ?? "Rạp phim",
                                showtime.Screen?.Theater?.Address ?? "",
                                showtime.Screen?.ScreenName ?? "Phòng chiếu",
                                showtime.StartTime,
                                DateTime.Now,
                                paymentCode.ToUpper(),
                                totalAmountPaid,
                                seatNames,
                                comboText // Combo bắp nước
                            );
                            Console.WriteLine("[WEBHOOK] ✅ Hoàn tất gọi hàm gửi email.");
                        }
                        else
                        {
                            Console.WriteLine("[WEBHOOK] ⚠️ Bỏ qua gửi email do thiếu thông tin (Email/Showtime/User)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WEBHOOK] ❌ LỖI NGHIÊM TRỌNG TRONG TASK GỬI MAIL: " + ex.Message);
                    if (ex.InnerException != null) Console.WriteLine("Inner: " + ex.InnerException.Message);
                }
            });

            // 6. Xoá lock và lưu vào Database
            _context.SeatLocks.RemoveRange(lockedSeats);
            await _context.SaveChangesAsync();

            // 7. Notify SignalR cho từng ghế để realtime cập nhật giao diện
            foreach (var lockItem in lockedSeats)
            {
                await _hub.Clients
                    .Group($"Showtime_{lockItem.ShowtimeId}")
                    .SendAsync("ReceiveSeatStatus", lockItem.SeatId, "Locked", -1); // -1 để báo hiệu đã thanh toán
            }

            return Ok(new { success = true, message = "Payment processed successfully" });
        }

        private string ExtractPaymentCode(string transferContent)
        {
            if (string.IsNullOrEmpty(transferContent)) return null;
            // Tìm chuỗi RF theo sau là 6 chữ số
            var match = Regex.Match(transferContent, @"RF\d{6}", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.ToUpper() : null;
        }
    }

    public class SePayTransaction
    {
        public string id { get; set; }
        public decimal transferAmount { get; set; }
        public string content { get; set; }
        public string referenceCode { get; set; }
        public string transactionDate { get; set; }
    }
}