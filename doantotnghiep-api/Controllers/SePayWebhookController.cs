using doantotnghiep_api.Data;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
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

        public SePayWebhookController(AppDbContext context, IHubContext<BookingHub> hub)
        {
            _context = context;
            _hub = hub;
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
                .Where(x => x.PaymentCode == paymentCode)
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
                    TotalAmount = lockItem.TotalAmount ?? 0 // Cần map đúng logic tiền từng ghế nếu cần
                };
                _context.Bookings.Add(booking);
            }

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