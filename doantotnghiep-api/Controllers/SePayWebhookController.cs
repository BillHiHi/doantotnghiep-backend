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
                message = "SePay Webhook endpoint is reachable!",
                url = Request.Path.ToString(),
                timestamp = DateTime.Now
            });
        }

        [HttpGet("debug-locks")]
        public async Task<IActionResult> DebugLocks()
        {
            var locks = await _context.SeatLocks
                .Select(l => new { l.SeatId, l.PaymentCode, l.ExpiryTime, l.UserId })
                .ToListAsync();
            return Ok(locks);
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
        public async Task<IActionResult> ReceiveWebhook([FromBody] System.Text.Json.JsonElement payload)
        {
            try
            {
                // ==========================================
                // 0. LOGGING DỮ LIỆU THÔ ĐỂ DEBUG
                // ==========================================
                string rawJson = payload.GetRawText();
                Console.WriteLine($"[WEBHOOK] 📥 NHẬN DỮ LIỆU THÔ: {rawJson}");

                // 1. Tách lấy các trường cần thiết an toàn
                string content = payload.TryGetProperty("content", out var pContent) ? pContent.ToString() : "";
                string referenceCode = payload.TryGetProperty("referenceCode", out var pRef) ? pRef.ToString() : "";

                decimal transferAmount = 0;
                if (payload.TryGetProperty("transferAmount", out var pAmount))
                {
                    decimal.TryParse(pAmount.ToString(), out transferAmount);
                }

                if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(referenceCode))
                {
                    Console.WriteLine("[WEBHOOK] ❌ Dữ liệu không chứa content hoặc referenceCode.");
                    return BadRequest("Invalid payload structure");
                }

                // 2. Tách lấy mã đơn hàng (RFxxxxxx)
                string paymentCode = ExtractPaymentCode(content) ?? ExtractPaymentCode(referenceCode);

                if (string.IsNullOrEmpty(paymentCode))
                {
                    Console.WriteLine($"[WEBHOOK] 🔍 Không thấy RF trong content. Thử tìm số 6-10 chữ số...");
                    var matchDigits = Regex.Match(content, @"(\d{6,10})");
                    if (matchDigits.Success)
                    {
                        paymentCode = "RF" + matchDigits.Groups[1].Value;
                    }
                }

                if (string.IsNullOrEmpty(paymentCode))
                {
                    Console.WriteLine($"[WEBHOOK] ❌ Không xác định được mã thanh toán từ nội dung: {content}");
                    return Ok(new { success = false, message = "Payment code not found" });
                }

                paymentCode = paymentCode.ToUpper().Replace(" ", "");
                Console.WriteLine($"[WEBHOOK] 🔍 Đang xử lý mã: {paymentCode} | Số tiền: {transferAmount}");

                // 3. Kiểm tra trùng lặp (Idempotency)
                bool isAlreadyPaid = await _context.Bookings
                    .AsNoTracking()
                    .AnyAsync(b => b.PaymentCode != null && b.PaymentCode.Trim().ToUpper() == paymentCode && (b.Status == "Paid" || b.Status == "Hoàn thành"));

                if (isAlreadyPaid)
                {
                    Console.WriteLine($"[WEBHOOK] ⚠️ Đơn hàng {paymentCode} đã được xử lý. Bỏ qua.");
                    return Ok(new { success = true, message = "Already processed" });
                }

                // 4. Tìm các ghế đang giữ
                var lockedSeats = await _context.SeatLocks.Where(x => x.PaymentCode != null).ToListAsync();
                var targetSeats = lockedSeats.Where(x => x.PaymentCode.Trim().ToUpper() == paymentCode).ToList();

                if (!targetSeats.Any())
                {
                    Console.WriteLine($"[WEBHOOK] ❌ KHÔNG TÌM THẤY GHẾ cho mã {paymentCode}. Danh sách mã trong DB: {string.Join(", ", lockedSeats.Select(s => s.PaymentCode))}");
                    return Ok(new { success = false, message = "No pending seats found" });
                }

                // 5. Kiểm tra số tiền (Tolerance 5000đ)
                decimal expectedAmount = targetSeats.Sum(s => s.TotalAmount ?? 0);
                if (Math.Abs(transferAmount - expectedAmount) > 5000)
                {
                    Console.WriteLine($"[WEBHOOK] 💰 Cảnh báo: Tiền lệch (Nhận: {transferAmount}, Cần: {expectedAmount})");
                }

                // 6. Xử lý lưu Booking trong Transaction
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var now = DateTime.UtcNow;
                        foreach (var lockItem in targetSeats)
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

                        // 7. Tặng điểm & Voucher
                        var firstLock = targetSeats.First();
                        await AwardPoints(firstLock.UserId, 10, $"Đặt vé phim (Mã: {paymentCode})");

                        if (firstLock.UserVoucherId.HasValue)
                        {
                            var uv = await _context.UserVouchers.FindAsync(firstLock.UserVoucherId.Value);
                            if (uv != null) { uv.IsUsed = true; uv.UsedAt = now; }
                        }

                        Console.WriteLine($"[WEBHOOK] ✅ Giao dịch {paymentCode} THÀNH CÔNG!");

                        // Snapshot dữ liệu để dùng cho Task chạy ngầm
                        var userIdForEmail = firstLock.UserId;
                        var showtimeIdForEmail = firstLock.ShowtimeId;
                        var finalPaymentCode = paymentCode;
                        var finalAmount = expectedAmount;
                        var combosJson = firstLock.Combos;

                        // Thông báo SignalR ngay lập tức cho các khách hàng khác
                        foreach (var item in targetSeats)
                        {
                            await _hub.Clients.Group($"Showtime_{item.ShowtimeId}").SendAsync("ReceiveSeatStatus", item.SeatId, "Booked", -1);
                        }

                        _context.SeatLocks.RemoveRange(targetSeats);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // 8. Gửi email & Tích điểm (Chỉ chạy SAU KHI COMMIT thành công)
                        _ = Task.Run(async () => {
                            Console.WriteLine($"[EMAIL-TASK] 🚀 Bắt đầu tiến trình gửi mail cho giao dịch {finalPaymentCode}...");
                            try {
                                using (var scope = _serviceProvider.CreateScope()) {
                                    var sc = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                    var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                                    
                                    var u = await sc.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userIdForEmail);
                                    var st = await sc.Showtimes.AsNoTracking()
                                        .Include(s => s.Movie)
                                        .Include(s => s.Screen).ThenInclude(scr => scr.Theater)
                                        .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeIdForEmail);
                                    
                                    if (u != null && st != null) {
                                        // Lấy danh sách tên ghế từ Bookings
                                        var bks = await sc.Bookings.AsNoTracking()
                                            .Include(b => b.Seat)
                                            .Where(b => b.PaymentCode == finalPaymentCode)
                                            .ToListAsync();
                                            
                                        string seatsNames = string.Join(", ", bks.Select(b => b.Seat.RowNumber + b.Seat.SeatNumber));
                                        
                                        // 🍿 Giải mã Combo bắp nước
                                        string comboText = "";
                                        if (!string.IsNullOrEmpty(combosJson)) {
                                            try {
                                                using var doc = System.Text.Json.JsonDocument.Parse(combosJson);
                                                var items = new List<string>();
                                                foreach (var item in doc.RootElement.EnumerateArray()) {
                                                    string name = item.GetProperty("name").GetString() ?? "";
                                                    int qty = item.GetProperty("qty").GetInt32();
                                                    if (qty > 0) items.Add($"{qty}x {name}");
                                                }
                                                comboText = string.Join(", ", items);
                                            } catch { comboText = combosJson; }
                                        }

                                        await email.SendTicketEmailAsync(u.Email, u.FullName??"KH", u.PhoneNumber??"", st.Movie?.Title??"Phim", st.Movie?.PosterUrl??"", st.Screen?.Theater?.Name??"Rạp", st.Screen?.Theater?.Address??"", st.Screen?.ScreenName??"Phòng", st.StartTime, DateTime.Now, finalPaymentCode, finalAmount, seatsNames, comboText);
                                        Console.WriteLine($"[EMAIL-TASK] ✅ Đã gửi email vé thành công tới {u.Email}");
                                    }
                                }
                            } catch(Exception ex){ 
                                Console.WriteLine("[EMAIL-TASK LỖI] ❌ " + ex.Message); 
                            }
                        });

                        return Ok(new { success = true });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK ERROR] ❌ Lỗi nghiêm trọng: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private string ExtractPaymentCode(string transferContent)
        {
            if (string.IsNullOrEmpty(transferContent)) return null;
            var match = Regex.Match(transferContent, @"RF[\s\-_]?(\d{6,10})", RegexOptions.IgnoreCase);
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