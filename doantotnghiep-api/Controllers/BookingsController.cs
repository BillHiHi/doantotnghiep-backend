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
            // fake mã thanh toán
            var paymentCode = Guid.NewGuid().ToString().Substring(0, 8);

            // dùng free QR generator
            var qrText = $"PAYMENT_{paymentCode}";
            var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={qrText}";

            return Ok(new PaymentResultDto
            {
                QrUrl = qrUrl,
                PaymentCode = paymentCode
            });
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

                var lockSeat = await _context.SeatLocks.FirstOrDefaultAsync(x =>
                    x.SeatId == request.SeatId &&
                    x.ShowtimeId == request.ShowtimeId &&
                    x.UserId == request.UserId);

                if (lockSeat == null)
                    return BadRequest("Ghế chưa được giữ");

                var booking = new Bookings
                {
                    UserId = request.UserId,
                    ShowtimeId = request.ShowtimeId,
                    SeatId = request.SeatId, // ⭐ FIX Ở ĐÂY
                    BookingDate = now,
                    Status = "Paid"
                };

                _context.Bookings.Add(booking);
                _context.SeatLocks.Remove(lockSeat);

                await _context.SaveChangesAsync();

                // ⭐ Notify SignalR: Seat is now permanently occupied
                await _hub.Clients
                    .Group($"Showtime_{request.ShowtimeId}")
                    .SendAsync("ReceiveSeatStatus", request.SeatId, "Locked", -1); // -1 or system ID to indicate it's not by a specific user anymore but general lock

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
