using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/vouchers")]
    public class VouchersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VouchersController(AppDbContext context)
        {
            _context = context;
        }

        // ================= GET VOUCHERS (FOR USERS TO REDEEM) =================
        [HttpGet]
        public async Task<IActionResult> GetVouchers()
        {
            var vouchers = await _context.Vouchers
                .Where(v => v.IsActive && (v.ExpiryDate == null || v.ExpiryDate > DateTime.UtcNow))
                .OrderBy(v => v.PointsRequired)
                .ToListAsync();
            return Ok(vouchers);
        }

        // ================= GET MY VOUCHERS =================
        [HttpGet("my-vouchers/{userId}")]
        public async Task<IActionResult> GetMyVouchers(int userId)
        {
            var userVouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId && !uv.IsUsed)
                .Select(uv => new {
                    uv.Id,
                    uv.VoucherId,
                    uv.Voucher.Title,
                    uv.Voucher.Code,
                    uv.Voucher.DiscountPercent,
                    uv.Voucher.VoucherType,
                    uv.Voucher.ExpiryDate,
                    uv.RedeemedAt
                })
                .ToListAsync();
            return Ok(userVouchers);
        }

        // ================= REDEEM VOUCHER =================
        [HttpPost("redeem")]
        public async Task<IActionResult> RedeemVoucher([FromBody] RedeemRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            var voucher = await _context.Vouchers.FindAsync(request.VoucherId);

            if (user == null || voucher == null) return NotFound("User hoặc Voucher không tồn tại");

            if (user.Points < voucher.PointsRequired)
                return BadRequest("Bạn không đủ điểm để đổi voucher này");

            if (!voucher.IsActive || (voucher.ExpiryDate != null && voucher.ExpiryDate < DateTime.UtcNow))
                return BadRequest("Voucher đã hết hạn hoặc không còn áp dụng");

            // Trừ điểm và tạo log
            user.Points -= voucher.PointsRequired;
            
            var pointTrans = new PointTransaction {
                UserId = user.UserId,
                Points = -voucher.PointsRequired,
                Description = $"Đổi voucher: {voucher.Title}",
                TransactionDate = DateTime.UtcNow
            };

            var userVoucher = new UserVoucher {
                UserId = user.UserId,
                VoucherId = voucher.VoucherId,
                IsUsed = false,
                RedeemedAt = DateTime.UtcNow
            };

            _context.PointTransactions.Add(pointTrans);
            _context.UserVouchers.Add(userVoucher);
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi voucher thành công!", currentPoints = user.Points });
        }

        // ================= ADMIN: GET ALL VOUCHERS =================
        [HttpGet("admin")]
        //[Authorize(Roles = "Admin,SUPER_ADMIN")]
        public async Task<IActionResult> GetAllVouchers()
        {
            var vouchers = await _context.Vouchers.OrderByDescending(v => v.CreatedAt).ToListAsync();
            return Ok(vouchers);
        }

        // ================= ADMIN: CREATE VOUCHER =================
        [HttpPost]
        public async Task<IActionResult> CreateVoucher([FromBody] Voucher voucher)
        {
            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();
            return Ok(voucher);
        }

        // ================= ADMIN: UPDATE VOUCHER =================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVoucher(int id, [FromBody] Voucher voucher)
        {
            if (id != voucher.VoucherId) return BadRequest();
            _context.Entry(voucher).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok(voucher);
        }

        // ================= ADMIN: DELETE VOUCHER =================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            var v = await _context.Vouchers.FindAsync(id);
            if (v == null) return NotFound();
            _context.Vouchers.Remove(v);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("apply-voucher")]
        public async Task<IActionResult> ApplyVoucher([FromBody] ApplyRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Code))
                    return BadRequest("Thông tin mã voucher không hợp lệ");

                var userVoucher = await _context.UserVouchers
                    .Include(uv => uv.Voucher)
                    .Where(uv => uv.Voucher != null && uv.Voucher.Code == request.Code && uv.UserId == request.UserId && !uv.IsUsed)
                    .FirstOrDefaultAsync();

                if (userVoucher == null || userVoucher.Voucher == null) 
                    return BadRequest("Mã voucher không hợp lệ hoặc bạn chưa sở hữu voucher này. Hãy đổi voucher bằng điểm trước khi áp dụng!");

                var voucher = userVoucher.Voucher;

                if (!voucher.IsActive || (voucher.ExpiryDate != null && voucher.ExpiryDate < DateTime.UtcNow))
                    return BadRequest("Voucher đã hết hạn");

                decimal discountAmount = 0;
                int percent = voucher.DiscountPercent;

                if (voucher.VoucherType == "Ticket")
                    discountAmount = request.SeatTotal * (decimal)percent / 100;
                else if (voucher.VoucherType == "Water")
                    discountAmount = request.ComboTotal * (decimal)percent / 100;
                else // All
                    discountAmount = (request.SeatTotal + request.ComboTotal) * (decimal)percent / 100;

                return Ok(new { 
                    success = true, 
                    discountAmount, 
                    newTotal = (request.SeatTotal + request.ComboTotal) - discountAmount,
                    voucherId = voucher.VoucherId,
                    userVoucherId = userVoucher.Id,
                    title = voucher.Title
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApplyVoucher Error]: {ex}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi áp dụng voucher", error = ex.Message });
            }
        }

        public class RedeemRequest {
            public int UserId { get; set; }
            public int VoucherId { get; set; }
        }

        public class ApplyRequest {
            public string Code { get; set; }
            public int UserId { get; set; }
            public decimal SeatTotal { get; set; }
            public decimal ComboTotal { get; set; }
        }
    }
}
