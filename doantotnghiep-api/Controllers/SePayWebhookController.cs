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
        private static readonly object _lockObject = new object();
        private static Random _random = new Random();

        public SePayWebhookController(AppDbContext context, IHubContext<BookingHub> hub, IEmailService emailService, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _context = context;
            _hub = hub;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult TestConnection()
        {
            return Ok(new
            {
                status = "Alive",
                message = "SePay Webhook endpoint is reachable! Please configure this URL in your SePay Dashboard.",
                url = Request.Path.ToString()
            });
        }

        private async Task AwardPoints(int userId, decimal totalAmount, string description)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                int points = 10;
                user.Points += points;

                var transaction = new PointTransaction
                {
                    UserId = userId,
                    Points = points,
                    Description = description,
                    TransactionDate = DateTime.UtcNow
                };

                _context.PointTransactions.Add(transaction);
                await _context.SaveChangesAsync();
            }
        }

        [HttpPost]
        [HttpPost("receive")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] SePayTransaction payload)
        {
            // ==========================================
            // 0. LOGGING VÀ BẢO MẬT (TẠM DISABLE API KEY ĐỂ TEST)
            // ==========================================
            var authHeader = Request.Headers["Authorization"].ToString();
            Console.WriteLine($"[WEBHOOK] 📥 Nhận request. Header Authorization: '{authHeader}'");

            /*
            var configuredApiKey = _configuration["SePaySettings:ApiKey"];
            var expectedHeader = $"Apikey {configuredApiKey}";
            if (!string.IsNullOrEmpty(configuredApiKey) &&
                (string.IsNullOrEmpty(authHeader) || !authHeader.Equals(expectedHeader, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("[WEBHOOK] ❌ Cảnh báo: Sai API Key hoặc chưa cấu hình đúng trên SePay Dashboard!");
                return Unauthorized(new { success = false, message = "Invalid API Key" });
            }
            */

            Console.WriteLine($"[WEBHOOK] 📥 Nhận dữ liệu: Content='{payload?.content}', Amount={payload?.transferAmount}");

            if (payload == null)
            {
                return BadRequest(new { success = false, message = "Invalid data" });
            }

            // 1. Tách lấy mã đơn hàng (RFxxxxxx)
            string paymentCode = ExtractPaymentCode(payload.content) ?? ExtractPaymentCode(payload.referenceCode);

            if (string.IsNullOrEmpty(paymentCode))
            {
                Console.WriteLine($"[WEBHOOK] ❌ Không tìm thấy mã RF. Content: '{payload.content}', RefCode: '{payload.referenceCode}'");
                return Ok(new { success = false, message = "Payment code not found" });
            }

            paymentCode = paymentCode.ToUpper();
            Console.WriteLine($"[WEBHOOK] 🔍 Đang xử lý mã: {paymentCode}");

            // ==========================================
            // BƯỚC KIỂM TRA TRÙNG LẶP (IDEMPOTENCY)
            // ==========================================
            bool isAlreadyPaid = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(b => b.PaymentCode == paymentCode && b.Status == "Paid");

            if (isAlreadyPaid)
            {
                Console.WriteLine($"[WEBHOOK] ⚠️ Đơn hàng {paymentCode} đã được xử lý trước đó. Bỏ qua.");
                return Ok(new { success = true, message = "Order already processed" });
            }

            // 3. Tìm các ghế đang giữ với mã này
            var lockedSeats = await _context.SeatLocks
                .Where(x => x.PaymentCode != null && x.PaymentCode.ToUpper() == paymentCode)
                .ToListAsync();

            if (!lockedSeats.Any())
            {
                Console.WriteLine($"[WEBHOOK] ❌ KHÔNG TÌM THẤY GHẾ cho mã {paymentCode}. Có thể ghế đã hết hạn giữ (10p) hoặc mã sai.");
                return Ok(new { success = false, message = "No pending seats found" });
            }

            Console.WriteLine($"[WEBHOOK] 📍 Tìm thấy {lockedSeats.Count} ghế đang được giữ.");

            // 4. Kiểm tra số tiền
            decimal amountIn = payload.transferAmount;
            decimal expectedAmount = lockedSeats.Sum(s => s.TotalAmount ?? 0);
            decimal tolerance = 5000;

            Console.WriteLine($"[WEBHOOK] 💰 Kiểm tra tiền: Nhận={amountIn}, Cần={expectedAmount} (Sai số cho phép: {tolerance})");

            if (Math.Abs(amountIn - expectedAmount) > tolerance)
            {
                Console.WriteLine($"[WEBHOOK] ❌ Sai lệch số tiền quá lớn. Chênh lệch: {Math.Abs(amountIn - expectedAmount)}");
                return Ok(new { success = false, message = "Insufficient amount" });
            }

            // ==========================================
            // SỬ DỤNG TRANSACTION
            // ==========================================
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var now = DateTime.UtcNow;

                    // 5. Chuyển từ SeatLock sang Bookings
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
                            Combos = lockItem.Combos,
                            UserVoucherId = lockItem.UserVoucherId
                        };
                        _context.Bookings.Add(booking);
                    }

                    await _context.SaveChangesAsync();

                    // 6. Tặng điểm & Voucher
                    var firstLock = lockedSeats.First();
                    await AwardPoints(firstLock.UserId, 10, $"Tích điểm đặt vé (Mã: {paymentCode})");

                    if (firstLock.UserVoucherId.HasValue)
                    {
                        var uv = await _context.UserVouchers.FindAsync(firstLock.UserVoucherId.Value);
                        if (uv != null) { uv.IsUsed = true; uv.UsedAt = now; }
                    }

                    // 7. Xoá lock
                    _context.SeatLocks.RemoveRange(lockedSeats);
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();
                    Console.WriteLine($"[WEBHOOK] ✅ Giao dịch {paymentCode} thành công!");

                    // 8. Gửi email ngầm
                    var targetUserId = firstLock.UserId;
                    var targetShowtimeId = firstLock.ShowtimeId;
                    var targetCombos = firstLock.Combos;
                    var targetPaymentCode = paymentCode;
                    var targetSeatIds = lockedSeats.Select(s => s.SeatId).ToList();
                    var targetTotalAmount = lockedSeats.Sum(s => s.TotalAmount ?? 0);

                    _ = Task.Run(async () => {
                        try
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                                var user = await scopedContext.Users.FindAsync(targetUserId);
                                var showtime = await scopedContext.Showtimes
                                    .Include(s => s.Movie)
                                    .Include(s => s.Screen).ThenInclude(sc => sc.Theater)
                                    .FirstOrDefaultAsync(s => s.ShowtimeId == targetShowtimeId);

                                if (user != null && showtime != null)
                                {
                                    var seats = await scopedContext.Seats.Where(s => targetSeatIds.Contains(s.SeatId)).ToListAsync();
                                    string seatNames = string.Join(", ", seats.Select(s => $"{s.RowNumber}{s.SeatNumber}"));

                                    await scopedEmailService.SendTicketEmailAsync(
                                        user.Email, user.FullName ?? "Khách hàng", user.PhoneNumber ?? "N/A",
                                        showtime.Movie.Title, showtime.Movie.PosterUrl,
                                        showtime.Screen.Theater.Name, showtime.Screen.Theater.Address, showtime.Screen.ScreenName,
                                        showtime.StartTime, DateTime.Now, targetPaymentCode, targetTotalAmount, seatNames, targetCombos
                                    );
                                }
                            }
                        }
                        catch (Exception ex) { Console.WriteLine("[WEBHOOK] 📧 Lỗi gửi email: " + ex.Message); }
                    });

                    // 9. Notify SignalR
                    foreach (var lockItem in lockedSeats)
                    {
                        await _hub.Clients.Group($"Showtime_{lockItem.ShowtimeId}")
                            .SendAsync("ReceiveSeatStatus", lockItem.SeatId, "Locked", -1);
                    }

                    return Ok(new { success = true });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"[WEBHOOK] ❌ Lỗi: {ex.Message}");
                    return StatusCode(500, new { success = false });
                }
            }
        }

        private string ExtractPaymentCode(string transferContent)
        {
            if (string.IsNullOrEmpty(transferContent)) return null;
            
            // Regex mới: Tìm chữ RF (không phân biệt hoa thường) 
            // theo sau có thể là dấu cách, dấu gạch ngang hoặc không có gì, 
            // và kết thúc bằng đúng 6 chữ số.
            var match = Regex.Match(transferContent, @"RF[\s\-_]?(\d{6})", RegexOptions.IgnoreCase);
            
            if (match.Success) 
            {
                return "RF" + match.Groups[1].Value;
            }
            return null;
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