using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
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

        public BookingsController(AppDbContext context, IHubContext<BookingHub> hub)
        {
            _context = context;
            _hub = hub;
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
                            TotalAmount = dto.TotalAmount
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

                // Kiểm tra xem đã có booking chưa (phòng trường hợp Webhook đã xử lý xong)
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

                var booking = new Bookings
                {
                    UserId = request.UserId,
                    ShowtimeId = request.ShowtimeId,
                    SeatId = request.SeatId,
                    BookingDate = now,
                    Status = "Paid",
                    TotalAmount = lockSeat.TotalAmount ?? 0
                };

                _context.Bookings.Add(booking);
                _context.SeatLocks.Remove(lockSeat);

                await _context.SaveChangesAsync();

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
    }
}
