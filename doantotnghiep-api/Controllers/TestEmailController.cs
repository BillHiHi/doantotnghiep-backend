using Microsoft.AspNetCore.Mvc;
using doantotnghiep_api.Services;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestEmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public TestEmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpGet("send")]
        public async Task<IActionResult> TestSend()
        {
            try
            {
                string testEmail = "tongminhthang14@gmail.com";
                
                // Giả lập thông tin vé đầy đủ để test
                await _emailService.SendTicketEmailAsync(
                    testEmail,
                    "Tống Minh Thắng",
                    "0987.654.321",
                    "Avengers: Endgame",
                    "https://m.media-amazon.com/images/M/MV5BMTc5MDE2ODcwNV5BMl5BanBnXkFtZTgwMzI2NzQ2NzM@._V1_.jpg",
                    "Cinema Star Center - Quận 1",
                    "123 Lê Lợi, Phường Bến Thành, Quận 1, TP.HCM",
                    "P01 - Digital 2D", // screenName
                    DateTime.Now.AddHours(2),
                    DateTime.Now,
                    "RF99HD88",
                    150000,
                    "H12, H13",
                    "2x Combo Bắp Nước, 1x Coca-Cola" // comboDetails
                );


                return Ok(new { message = $"Đã gửi email test thành công tới {testEmail}. Vui lòng kiểm tra hộp thư (bao gồm cả thư rác)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
