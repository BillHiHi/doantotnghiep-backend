using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PointsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PointsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserPoints(int userId)
        {
            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => new { u.UserId, u.Points, u.FullName })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Người dùng không tồn tại");

            return Ok(user);
        }

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetPointHistory(int userId)
        {
            var history = await _context.PointTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return Ok(history);
        }
    }
}
