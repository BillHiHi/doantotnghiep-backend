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

            // 3. Tìm các ghế đang giữ với mã này
            var lockedSeats = await _context.SeatLocks
                .Where(x => x.PaymentCode == paymentCode && x.ExpiryTime > DateTime.UtcNow)
                .ToListAsync();

            if (!lockedSeats.Any())
            {
                return Ok(new { success = false, message = "No pending seats found for this code" });
            }

            // 4. Kiểm tra số tiền (Tuỳ chọn: có thể bỏ qua nếu muốn linh hoạt, hoặc check khớp TotalAmount)
            // decimal amountIn = payload.transferAmount;
            // if (amountIn < lockedSeats.First().TotalAmount) ...

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

            // GỬI EMAIL XÁC NHẬN (Lấy thông tin từ danh sách ghế vừa mua)
            var firstLock = lockedSeats.First();
            var seatIds = lockedSeats.Select(s => s.SeatId).ToList();
            
            _ = Task.Run(async () => {
                try {
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

                            if (!string.IsNullOrEmpty(posterUrl) && !posterUrl.StartsWith("http")) {
                                posterUrl = baseUrl.TrimEnd('/') + "/" + posterUrl.Replace("wwwroot/", "").TrimStart('/');
                            }

                            var seats = await scopedContext.Seats.Where(s => seatIds.Contains(s.SeatId)).ToListAsync();
                            string seatNames = string.Join(", ", seats.Select(s => $"{s.RowNumber}{s.SeatNumber}"));
                            decimal totalAmountPaid = lockedSeats.Sum(s => s.TotalAmount ?? 0);

                            // Giải mã JSON combo bắp nước
                            string comboText = "";
                            if (!string.IsNullOrEmpty(firstLock.Combos)) {
                                try {
                                    using (var doc = System.Text.Json.JsonDocument.Parse(firstLock.Combos)) {
                                        var items = new List<string>();
                                        foreach (var item in doc.RootElement.EnumerateArray()) {
                                            string name = item.GetProperty("name").GetString();
                                            int qty = item.GetProperty("qty").GetInt32();
                                            if (qty > 0) items.Add($"{qty}x {name}");
                                        }
                                        comboText = string.Join(", ", items);
                                    }
                                } catch { 
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
                                showtime.Screen?.ScreenName ?? "Phòng chiếu", // Phòng vé
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
                } catch (Exception ex) {
                    Console.WriteLine("[WEBHOOK] ❌ LỖI NGHIÊM TRỌNG TRONG TASK GỬI MAIL: " + ex.Message);
                    if (ex.InnerException != null) Console.WriteLine("Inner: " + ex.InnerException.Message);
                }
            });



            // 6. Xoá lock
            _context.SeatLocks.RemoveRange(lockedSeats);
            await _context.SaveChangesAsync();


            // 7. Notify SignalR cho từng ghế
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