using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<BookingHub> _hub;
        private readonly IEmailService _emailService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public BookingsController(AppDbContext context, IHubContext<BookingHub> hub, IEmailService emailService, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _context = context;
            _hub = hub;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        // =============================================
        // HOLD SEAT
        // =============================================
        [HttpPost("hold-seats")]
        public async Task<IActionResult> HoldSeats([FromBody] SeatHoldRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request null");

                var now = DateTime.UtcNow;

                // ⭐ CHECK USER (FIX FOREIGN KEY ERROR)
                var userExists = await _context.Users
                    .AnyAsync(x => x.UserId == request.UserId);

                if (!userExists)
                    return BadRequest("User không tồn tại");

                // cleanup expired
                var expired = await _context.SeatLocks
                    .Where(x => x.ExpiryTime < now)
                    .ToListAsync();

                if (expired.Any())
                {
                    _context.SeatLocks.RemoveRange(expired);
                    await _context.SaveChangesAsync();
                }

                // check seat
                if (!await _context.Seats.AnyAsync(x => x.SeatId == request.SeatId))
                    return BadRequest("Seat không tồn tại");

                if (!await _context.Showtimes.AnyAsync(x => x.ShowtimeId == request.ShowtimeId))
                    return BadRequest("Showtime không tồn tại");

                // Giới hạn 8 ghế
                var currentLocksCount = await _context.SeatLocks.CountAsync(x =>
                    x.UserId == request.UserId &&
                    x.ShowtimeId == request.ShowtimeId &&
                    x.ExpiryTime > now);

                if (currentLocksCount >= 8)
                    return BadRequest("Vui lòng thanh toán để chọn thêm ghế");

                var locked = await _context.SeatLocks.AnyAsync(x =>
                    x.SeatId == request.SeatId &&
                    x.ShowtimeId == request.ShowtimeId &&
                    x.ExpiryTime > now);

                if (locked)
                    return BadRequest("Seat đã bị giữ");

                var seatLock = new SeatLock
                {
                    SeatId = request.SeatId,
                    ShowtimeId = request.ShowtimeId,
                    UserId = request.UserId,
                    LockedAt = now,
                    ExpiryTime = now.AddMinutes(10)
                };

                _context.SeatLocks.Add(seatLock);
                await _context.SaveChangesAsync();

                await _hub.Clients
                    .Group($"Showtime_{request.ShowtimeId}")
                    .SendAsync("ReceiveSeatStatus", request.SeatId, "Locked", request.UserId);

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        // =============================================
        // RELEASE
        // =============================================
        [HttpPost("release-seats")]
        public async Task<IActionResult> ReleaseSeats([FromBody] SeatHoldRequest request)
        {
            try
            {
                var lockSeat = await _context.SeatLocks.FirstOrDefaultAsync(x =>
                    x.SeatId == request.SeatId &&
                    x.ShowtimeId == request.ShowtimeId &&
                    x.UserId == request.UserId);

                if (lockSeat == null)
                    return Ok();

                _context.SeatLocks.Remove(lockSeat);
                await _context.SaveChangesAsync();

                await _hub.Clients
                    .Group($"Showtime_{request.ShowtimeId}")
                    .SendAsync("ReceiveSeatStatus", request.SeatId, "Released", request.UserId);

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        // =============================================
        // CREATE FAKE QR PAYMENT
        // =============================================
        [HttpPost("create-payment")]
        public async Task<IActionResult> CreatePayment([FromBody] PaymentRequestDto dto)
        {
            try
            {
                if (dto == null || dto.SeatIds == null || !dto.SeatIds.Any())
                    return BadRequest("Thiếu thông tin ghế");

                var now = DateTime.UtcNow;

                // tạo mã thanh toán duy nhất: ví dụ RF123456
                var paymentCode = "RF" + new Random().Next(100000, 999999).ToString();
                
                // Lock ghế ngay khi chuẩn bị thanh toán
                foreach (var seatId in dto.SeatIds)
                {
                    // Check if already locked by someone else
                    var isLocked = await _context.SeatLocks.AnyAsync(x =>
                        x.SeatId == seatId &&
                        x.ShowtimeId == dto.ShowtimeId &&
                        x.ExpiryTime > now &&
                        x.UserId != dto.UserId);

                    if (isLocked)
                        return BadRequest($"Ghế ID {seatId} đã bị người khác giữ");

                    // Nếu mình đã giữ rồi thì update expiry và PaymentCode, chưa thì tạo mới
                    var myLock = await _context.SeatLocks.FirstOrDefaultAsync(x =>
                        x.SeatId == seatId &&
                        x.ShowtimeId == dto.ShowtimeId &&
                        x.UserId == dto.UserId);

                    if (myLock != null)
                    {
                        myLock.ExpiryTime = now.AddMinutes(15); // Tăng thời gian chờ thanh toán
                        myLock.PaymentCode = paymentCode;
                        myLock.TotalAmount = dto.TotalAmount;
                        myLock.Combos = dto.Combos;
                    }
                    else
                    {
                        var seatLock = new SeatLock
                        {
                            SeatId = seatId,
                            ShowtimeId = dto.ShowtimeId,
                            UserId = dto.UserId,
                            LockedAt = now,
                            ExpiryTime = now.AddMinutes(15),
                            PaymentCode = paymentCode,
                            TotalAmount = dto.TotalAmount,
                            Combos = dto.Combos
                        };
                        _context.SeatLocks.Add(seatLock);
                    }
                }

                await _context.SaveChangesAsync();

                // Notify SignalR
                foreach (var seatId in dto.SeatIds)
                {
                    await _hub.Clients
                        .Group($"Showtime_{dto.ShowtimeId}")
                        .SendAsync("ReceiveSeatStatus", seatId, "Locked", dto.UserId);
                }

                // Sử dụng định dạng VietQR để khách hàng quét mã là có sẵn STK, Số tiền và Nội dung
                // Bạn hãy thay ACCOUNT_NUMBER và BANK_NAME bằng thông tin của bạn
                // Hoặc sử dụng dịch vụ của SePay: https://qr.sepay.vn/img?acc=STK&bank=NGAN_HANG&amount=TIEN&des=NOI_DUNG
                var bankAccount = "YOU_STK"; 
                var bankName = "YOUR_BANK";
                var qrUrl = $"https://qr.sepay.vn/img?acc={bankAccount}&bank={bankName}&amount={dto.TotalAmount}&des={paymentCode}";

                return Ok(new PaymentResultDto
                {
                    QrUrl = qrUrl,
                    PaymentCode = paymentCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        // =============================================
        // CONFIRM PAYMENT (FAKE SUCCESS)
        // =============================================
        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment([FromBody] SeatHoldRequest request)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (request == null)
                    return BadRequest("Request null");

                // Kiểm tra xem đã có booking chưa
                var existingBooking = await _context.Bookings.AnyAsync(x =>
                    x.SeatId == request.SeatId &&
                    x.ShowtimeId == request.ShowtimeId &&
                    x.Status == "Paid");

                if (existingBooking)
                    return Ok("Thanh toán thành công (đã xác nhận trước đó)");

                var lockSeat = await _context.SeatLocks.FirstOrDefaultAsync(x =>
                    x.SeatId == request.SeatId &&
                    x.ShowtimeId == request.ShowtimeId &&
                    x.UserId == request.UserId);

                if (lockSeat == null)
                    return BadRequest("Ghế chưa được giữ hoặc đã hết hạn");

                var paymentCode = lockSeat.PaymentCode;

                var booking = new Bookings
                {
                    UserId = request.UserId,
                    ShowtimeId = request.ShowtimeId,
                    SeatId = request.SeatId,
                    BookingDate = now,
                    Status = "Paid",
                    TotalAmount = lockSeat.TotalAmount ?? 0,
                    PaymentCode = lockSeat.PaymentCode,
                    Combos = lockSeat.Combos
                };

                _context.Bookings.Add(booking);
                _context.SeatLocks.Remove(lockSeat);

                await _context.SaveChangesAsync();

                // GỬI EMAIL XÁC NHẬN (Khi ghế cuối cùng trong cùng 1 mã thanh toán được xác nhận)
                var remainingLocks = await _context.SeatLocks.AnyAsync(x => 
                    x.UserId == request.UserId && 
                    x.ShowtimeId == request.ShowtimeId && 
                    x.PaymentCode == paymentCode);

                if (!remainingLocks)
                {
                    // Chạy task gửi email mà không làm chậm request chính
                    _ = Task.Run(async () => {
                        try {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                                var user = await scopedContext.Users.FindAsync(request.UserId);
                                var showtime = await scopedContext.Showtimes
                                    .Include(s => s.Movie)
                                    .Include(s => s.Screen)
                                        .ThenInclude(sc => sc.Theater)
                                    .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId);

                                if (user != null && !string.IsNullOrEmpty(user.Email) && showtime != null)
                                {
                                    // Lấy danh sách tên ghế
                                    var paidSeats = await scopedContext.Bookings
                                        .Include(b => b.Seat)
                                        .Where(b => b.UserId == request.UserId && b.ShowtimeId == request.ShowtimeId && b.Status == "Paid")
                                        .ToListAsync();
                                    
                                    string seatNames = string.Join(", ", paidSeats.Select(s => $"{s.Seat.RowNumber}{s.Seat.SeatNumber}"));
                                    decimal totalPaid = paidSeats.Sum(s => s.TotalAmount);

                                    var movie = showtime.Movie;
                                    var posterUrl = movie?.PosterUrl;

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
                                        paymentCode ?? "N/A",
                                        totalPaid,
                                        seatNames,
                                        "" // Combo trống cho confirm đơn lẻ
                                    );
                                }
                            }
                        } catch (Exception ex) {
                            Console.WriteLine("Email Error: " + ex.Message);
                        }
                    });
                }



                // Notify SignalR: Seat is now permanently occupied
                await _hub.Clients
                    .Group($"Showtime_{request.ShowtimeId}")
                    .SendAsync("ReceiveSeatStatus", request.SeatId, "Locked", -1);

                return Ok("Thanh toán thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500, ex.ToString());
            }
        }
        // =============================================
        // CONFIRM ALL PAYMENT (BULK)
        // =============================================
        [HttpPost("confirm-all-payment")]
        public async Task<IActionResult> ConfirmAllPayment([FromBody] ConfirmAllRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.PaymentCode))
                    return BadRequest("Thiếu thông tin xác nhận");

                var now = DateTime.UtcNow;

                // 1. Tìm các ghế đang giữ với mã này
                var lockedSeats = await _context.SeatLocks
                    .Where(x => x.PaymentCode == request.PaymentCode && x.UserId == request.UserId && x.ShowtimeId == request.ShowtimeId)
                    .ToListAsync();

                if (!lockedSeats.Any())
                {
                    // Kiểm tra xem đã có booking chưa (Trường hợp nhấn 2 lần)
                    var hasBooking = await _context.Bookings.AnyAsync(x =>
                        x.UserId == request.UserId &&
                        x.ShowtimeId == request.ShowtimeId &&
                        x.Status == "Paid");

                    if (hasBooking) return Ok("Thanh toán thành công (đã xác nhận trước đó)");

                    return BadRequest("Không tìm thấy thông tin giữ ghế hoặc mã thanh toán không đúng");
                }

                // 2. Chuyển từ SeatLock sang Bookings
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

                // 3. Xoá lock
                _context.SeatLocks.RemoveRange(lockedSeats);
                await _context.SaveChangesAsync();

                // 4. GỬI EMAIL XÁC NHẬN (logic giống bên webhook)
                var seatIds = lockedSeats.Select(s => s.SeatId).ToList();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                            var user = await scopedContext.Users.FindAsync(request.UserId);
                            var showtime = await scopedContext.Showtimes
                                .Include(s => s.Movie)
                                .Include(s => s.Screen)
                                    .ThenInclude(sc => sc.Theater)
                                .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId);

                            if (user != null && !string.IsNullOrEmpty(user.Email) && showtime != null)
                            {
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

                                var firstLock = lockedSeats.FirstOrDefault();
                                // Giải mã JSON combo bắp nước
                                string comboText = "";
                                if (firstLock != null && !string.IsNullOrEmpty(firstLock.Combos)) {
                                    try {
                                        using (var doc = System.Text.Json.JsonDocument.Parse(firstLock.Combos)) {
                                            var items = new List<string>();
                                            foreach (var item in doc.RootElement.EnumerateArray()) {
                                                string name = item.GetProperty("name").GetString() ?? "";
                                                int qty = item.GetProperty("qty").GetInt32();
                                                if (qty > 0) items.Add($"{qty}x {name}");
                                            }
                                            comboText = string.Join(", ", items);
                                        }
                                    } catch { 
                                        comboText = firstLock?.Combos ?? "";
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
                                    request.PaymentCode.ToUpper(),
                                    totalAmountPaid,
                                    seatNames,
                                    comboText
                                );
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Manual confirm Email Error: " + ex.Message);
                    }
                });

                // 5. Notify SignalR cho từng ghế
                foreach (var seatId in seatIds)
                {
                    await _hub.Clients
                        .Group($"Showtime_{request.ShowtimeId}")
                        .SendAsync("ReceiveSeatStatus", seatId, "Locked", -1);
                }

                return Ok("Thanh toán thành công");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        // =============================================
        // SCAN TICKET BY PAYMENT CODE
        // =============================================
        [HttpGet("scan/{paymentCode}")]
        public async Task<IActionResult> ScanTicket(string paymentCode)
        {
            try
            {
                var bookings = await _context.Bookings
                    .Include(b => b.Seat)
                    .Where(b => b.PaymentCode == paymentCode)
                    .ToListAsync();

                if (!bookings.Any())
                    return NotFound(new { message = $"Không tìm thấy đơn hàng với mã {paymentCode}" });

                var firstBooking = bookings.First();
                var user = await _context.Users.FindAsync(firstBooking.UserId);
                var showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Screen)
                        .ThenInclude(sc => sc.Theater)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == firstBooking.ShowtimeId);

                string seatNames = string.Join(", ", bookings.Select(s => $"{s.Seat.RowNumber}{s.Seat.SeatNumber}"));
                decimal totalAmount = bookings.Sum(b => b.TotalAmount) / bookings.Count(); // Wait, TotalAmount in Booking is usually total per lock

                // Giả sử TotalAmount lưu đúng (nếu mỗi booking lưu tổng tiền, ta lấy của cái đầu)
                totalAmount = firstBooking.TotalAmount;

                var movie = showtime?.Movie;
                var posterUrl = movie?.PosterUrl;
                var baseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5066";
                if (!string.IsNullOrEmpty(posterUrl) && !posterUrl.StartsWith("http"))
                {
                    posterUrl = baseUrl.TrimEnd('/') + "/" + posterUrl.Replace("wwwroot/", "").TrimStart('/');
                }

                return Ok(new
                {
                    code = paymentCode,
                    customerName = user?.FullName ?? "Khách hàng",
                    customerPhone = user?.PhoneNumber ?? "",
                    movie = movie?.Title ?? "Phim",
                    poster = posterUrl ?? "",
                    date = showtime?.StartTime.ToString("dd/MM/yyyy"),
                    time = showtime?.StartTime.ToString("HH:mm"),
                    theater = showtime?.Screen?.Theater?.Name ?? "Rạp phim",
                    screen = showtime?.Screen?.ScreenName ?? "Phòng chiếu",
                    seats = seatNames,
                    total = totalAmount,
                    status = firstBooking.Status == "Paid" ? "PAID" : (firstBooking.Status == "Collected" ? "COLLECTED" : firstBooking.Status)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        // =============================================
        // GET RECENT TICKETS (Lịch sử giao dịch)
        // =============================================
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentTickets()
        {
            try
            {
                // Lấy các payment code mới nhất (những booking thành công)
                var recentCodes = await _context.Bookings
                    .Where(b => b.PaymentCode != null)
                    .OrderByDescending(b => b.BookingDate)
                    .Select(b => b.PaymentCode)
                    .Distinct()
                    .Take(10)
                    .ToListAsync();

                var results = new List<object>();

                foreach (var code in recentCodes)
                {
                    var bookings = await _context.Bookings
                        .Include(b => b.Seat)
                        .Where(b => b.PaymentCode == code)
                        .ToListAsync();

                    if (bookings.Any())
                    {
                        var firstBooking = bookings.First();
                        var user = await _context.Users.FindAsync(firstBooking.UserId);
                        var showtime = await _context.Showtimes
                            .Include(s => s.Movie)
                            .FirstOrDefaultAsync(s => s.ShowtimeId == firstBooking.ShowtimeId);

                        results.Add(new
                        {
                            code = code,
                            customerName = user?.FullName ?? "Khách hàng",
                            movie = showtime?.Movie?.Title ?? "Phim",
                            time = showtime?.StartTime.ToString("HH:mm"),
                            status = firstBooking.Status == "Paid" ? "PAID" : (firstBooking.Status == "Collected" ? "COLLECTED" : firstBooking.Status)
                        });
                    }
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }

        // =============================================
        // COLLECT TICKET (Phát vé)
        // =============================================
        [HttpPost("collect/{paymentCode}")]
        public async Task<IActionResult> CollectTicket(string paymentCode)
        {
            try
            {
                var bookings = await _context.Bookings
                    .Where(b => b.PaymentCode == paymentCode)
                    .ToListAsync();

                if (!bookings.Any())
                    return NotFound("Không tìm thấy đơn hàng");

                foreach (var booking in bookings)
                {
                    booking.Status = "Collected";
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Phát vé bộ phận thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }
    }
}
