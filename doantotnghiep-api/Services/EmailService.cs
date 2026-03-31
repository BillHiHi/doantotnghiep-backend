using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace doantotnghiep_api.Services
{
    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? AppBaseUrl { get; set; }
    }

    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _settings = configuration.GetSection("SmtpSettings").Get<SmtpSettings>() ?? new SmtpSettings();
            if (string.IsNullOrEmpty(_settings.AppBaseUrl)) _settings.AppBaseUrl = configuration["AppBaseUrl"];
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                if (string.IsNullOrEmpty(_settings.Username) || string.IsNullOrEmpty(_settings.Password))
                {
                    throw new Exception("Cấu hình SMTP trống (Username/Password)");
                }

                using var client = new SmtpClient(_settings.Server, _settings.Port)
                {
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    EnableSsl = true,
                    Timeout = 10000 // 10 giây
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"[EMAIL] ✅ Gửi thành công tới: {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL] ❌ LỖI GỬI MAIL tới {toEmail}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[EMAIL] Inner: {ex.InnerException.Message}");
                }
                throw; // Đưa lỗi ra ngoài để Controller bắt được
            }
        }

        public async Task SendTicketEmailAsync(string toEmail, string fullName, string phoneNumber, string movieTitle, string posterUrl, string theaterName, string theaterAddress, string screenName, DateTime showtime, DateTime bookingDate, string paymentCode, decimal totalAmount, string seats, string comboDetails)
        {
            var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={paymentCode}";
            var subject = $"Vé Cinema: {movieTitle} - {paymentCode}";
            var baseUrl = _settings.AppBaseUrl ?? _configuration["AppBaseUrl"] ?? "https://doantotnghiep-backend-whqz.onrender.com"; 
            
            if (string.IsNullOrEmpty(posterUrl)) 
                posterUrl = "https://placehold.co/300x450?text=No+Poster";
            else if (!posterUrl.StartsWith("http"))
            {
                // Clean path like what we did on frontend
                var cleanPath = posterUrl.Replace("wwwroot/", "").Replace("uploads/", "").TrimStart('/').Replace(" ", "%20");
                posterUrl = $"{baseUrl.TrimEnd('/')}/uploads/{cleanPath}";
            }
            
            Console.WriteLine($"[EMAIL] 🎞️ Poster URL: {posterUrl}");

            var body = $@"
<div style='background-color:#f8fafc; padding:30px; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; color:#1e293b; line-height:1.5;'>
    <div style='max-width:600px; margin:0 auto; background:#fff; border-radius:12px; overflow:hidden; box-shadow:0 10px 15px -3px rgba(0,0,0,0.1);'>
        
        <!-- Header -->
        <div style='background:#1e1b4b; padding:40px 20px; text-align:center;'>
            <h1 style='color:#fbbf24; margin:0; font-size:24px; letter-spacing:4px; text-transform:uppercase;'>Cinema Ticket</h1>
            <p style='color:#94a3b8; margin:10px 0 0; font-size:13px;'>Cảm ơn bạn đã lựa chọn dịch vụ của chúng tôi</p>
        </div>
        
        <div style='padding:30px;'>
            <!-- Xin chào -->
            <h2 style='color:#0f172a; margin:0 0 10px 0; font-size:20px;'>Xin chào {fullName},</h2>
            <p style='color:#64748b; margin:0 0 30px 0; font-size:15px;'>Giao dịch của bạn đã được xác nhận thành công.</p>

            <!-- Thông tin khách hàng -->
            <div style='margin-bottom:30px;'>
                <h4 style='text-transform:uppercase; color:#94a3b8; font-size:12px; margin:0 0 10px 0; letter-spacing:1px; border-bottom:1px solid #f1f5f9; padding-bottom:5px;'>Thông tin người đặt</h4>
                <table width='100%' style='font-size:14px; border-collapse:collapse;'>
                    <tr>
                        <td width='120' style='color:#64748b; padding:6px 0;'>Họ tên:</td>
                        <td style='color:#1e293b; font-weight:600; padding:6px 0;'>{fullName}</td>
                    </tr>
                    <tr>
                        <td style='color:#64748b; padding:6px 0;'>Số điện thoại:</td>
                        <td style='color:#1e293b; padding:6px 0;'>{phoneNumber}</td>
                    </tr>
                    <tr>
                        <td style='color:#64748b; padding:6px 0;'>Email:</td>
                        <td style='color:#1e293b; padding:6px 0;'>{toEmail}</td>
                    </tr>
                </table>
            </div>

            <!-- Thông tin vé -->
            <div style='background:#f8fafc; border:1px solid #e2e8f0; border-radius:10px; padding:20px; margin-bottom:20px;'>
                <h4 style='text-transform:uppercase; color:#94a3b8; font-size:12px; margin:0 0 15px 0; letter-spacing:1px; text-align:center;'>Thông tin vé xem phim</h4>
                <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                        <td width='120' valign='top'>
                            <img src='{posterUrl}' style='width:100px; border-radius:8px; box-shadow:0 4px 6px -1px rgba(0,0,0,0.1);' alt='Poster'>
                        </td>
                        <td valign='top' style='padding-left:20px;'>
                            <h3 style='margin:0 0 12px 0; color:#1e1b4b; font-size:18px; line-height:1.3;'>{movieTitle}</h3>
                            
                            <div style='font-size:14px; color:#334155;'>
                                <p style='margin:6px 0;'><strong>Rạp:</strong> {theaterName}</p>
                                <p style='margin:4px 0 10px 0; font-size:12px; color:#64748b;'>{theaterAddress}</p>
                                <p style='margin:6px 0;'><strong>Phòng chiếu:</strong> {screenName}</p>
                                <p style='margin:6px 0;'><strong>Giờ chiếu:</strong> <span style='color:#0369a1; font-weight:600;'>{showtime:HH:mm} | {showtime:dd/MM/yyyy}</span></p>
                                <p style='margin:6px 0;'><strong>Vị trí ghế:</strong> <span style='color:#e11d48; font-weight:bold;'>{seats}</span></p>
                            </div>
                        </td>
                    </tr>
                </table>
            </div>

            <!-- Combo -->
            {(string.IsNullOrEmpty(comboDetails) ? "" : $@"
            <div style='background:#fffbeb; border:1px solid #fde68a; border-radius:10px; padding:15px; margin-bottom:20px;'>
                <p style='margin:0; font-size:14px; color:#92400e;'><strong>Combo bắp nước:</strong> {comboDetails}</p>
            </div>
            ")}

            <!-- Tổng tiền -->
            <div style='text-align:right; padding:15px 0; border-top:1px solid #f1f5f9;'>
                <span style='color:#64748b; font-size:14px;'>Tổng cộng:</span>
                <span style='color:#e11d48; font-size:22px; font-weight:800; margin-left:10px;'>{totalAmount:N0} VNĐ</span>
            </div>

            <!-- QR Code -->
            <div style='text-align:center; padding-top:20px; margin-top:10px; border-top:2px dashed #f1f5f9;'>
                <p style='margin:0 0 15px 0; color:#64748b; font-size:13px;'>Mã QR check-in tại quầy vé</p>
                <div style='display:inline-block; padding:15px; background:#fff; border:1px solid #e2e8f0; border-radius:8px;'>
                    <img src='{qrUrl}' width='140' height='140' style='display:block;' alt='QR Code'>
                </div>
                <div style='margin-top:10px;'>
                    <code style='background:#f1f5f9; padding:5px 15px; border-radius:4px; font-size:18px; font-weight:700; color:#0f172a; letter-spacing:2px;'>{paymentCode}</code>
                </div>
                <p style='margin:10px 0 0 0; color:#94a3b8; font-size:11px;'>Vui lòng cung cấp mã này cho nhân viên tại rạp để nhận vé giấy</p>
            </div>
        </div>
        
        <!-- Footer -->
        <div style='background:#f8fafc; padding:20px; text-align:center; font-size:12px; color:#94a3b8; border-top:1px solid #f1f5f9;'>
            <p style='margin:0;'>&copy; {DateTime.Now.Year} Cinema Booking System. All rights reserved.</p>
        </div>
    </div>
</div>";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}

