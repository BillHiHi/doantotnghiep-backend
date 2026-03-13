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
    }

    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;

        public EmailService(IConfiguration configuration)
        {
            _settings = configuration.GetSection("SmtpSettings").Get<SmtpSettings>() ?? new SmtpSettings();
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_settings.Server, _settings.Port)
                {
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    EnableSsl = true
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
                Console.WriteLine($"✅ Email sent to {toEmail} successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending email to {toEmail}: {ex.Message}");
            }
        }

        public async Task SendTicketEmailAsync(string toEmail, string fullName, string phoneNumber, string movieTitle, string posterUrl, string theaterName, string theaterAddress, DateTime showtime, DateTime bookingDate, string paymentCode, decimal totalAmount, string seats)
        {
            var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={paymentCode}";
            
            var subject = $"🎟️ Xác nhận đặt vé thành công: {movieTitle}";
            var body = $@"
                <div style='font-family: ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 650px; margin: 0 auto; background-color: #f4f7f9; padding: 20px;'>
                    <div style='background: #fff; border-radius: 20px; overflow: hidden; box-shadow: 0 15px 45px rgba(0,0,0,0.1);'>
                        
                        <!-- Header Background -->
                        <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 40px 20px; text-align: center;'>
                            <h1 style='color: #fbbf24; margin: 0; font-size: 30px; letter-spacing: 2px; text-transform: uppercase;'>Cinema Ticket</h1>
                            <p style='color: #94a3b8; font-size: 14px; margin-top: 10px;'>Cảm ơn bạn đã tin dùng dịch vụ của chúng tôi</p>
                        </div>

                        <div style='padding: 40px;'>
                            <h2 style='color: #1e293b; margin: 0 0 10px; font-size: 24px;'>Xin chào, {fullName}</h2>
                            <p style='color: #64748b; margin: 0 0 30px; font-size: 16px;'>Thông tin đơn hàng của bạn đã được xác nhận thành công vào lúc <strong>{bookingDate:HH:mm dd/MM/yyyy}</strong>.</p>

                            <!-- Ticket Info Section -->
                            <div style='background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 16px; display: table; width: 100%; border-collapse: separate;'>
                                <div style='display: table-row;'>
                                    <!-- Poster -->
                                    <div style='display: table-cell; width: 30%; vertical-align: top; padding: 20px;'>
                                        <img src='{posterUrl}' alt='{movieTitle}' style='width: 100%; border-radius: 10px; box-shadow: 0 4px 10px rgba(0,0,0,0.1);'>
                                    </div>
                                    
                                    <!-- Details -->
                                    <div style='display: table-cell; width: 70%; vertical-align: top; padding: 20px;'>
                                        <h3 style='margin: 0 0 15px; color: #0f172a; font-size: 22px; line-height: 1.2;'>{movieTitle}</h3>
                                        
                                        <table style='width: 100%; font-size: 14px; border-collapse: collapse;'>
                                            <tr>
                                                <td style='padding: 8px 0; color: #64748b; width: 100px;'>📍 Địa điểm:</td>
                                                <td style='padding: 8px 0; color: #1e293b;'><strong>{theaterName}</strong><br><small style='color: #94a3b8;'>{theaterAddress}</small></td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #64748b;'>🕒 Thời gian:</td>
                                                <td style='padding: 8px 0; color: #1e293b;'><strong>{showtime:HH:mm} | {showtime:dd/MM/yyyy}</strong></td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #64748b;'>💺 Ghế:</td>
                                                <td style='padding: 8px 0; color: #1e293b;'><strong>{seats}</strong></td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 8px 0; color: #64748b;'>📞 Liên hệ:</td>
                                                <td style='padding: 8px 0; color: #1e293b;'>{phoneNumber}</td>
                                            </tr>
                                        </table>
                                    </div>
                                </div>
                            </div>

                            <!-- Payment Summary -->
                            <div style='margin-top: 30px; padding: 20px; border-top: 2px dashed #e2e8f0; text-align: right;'>
                                <span style='color: #64748b; font-size: 16px;'>Tổng thanh toán: </span>
                                <span style='color: #ef4444; font-size: 24px; font-weight: 800;'>{totalAmount:N0} VNĐ</span>
                            </div>

                            <div style='margin-top: 40px; text-align: center;'>
                                <div style='display: inline-block; background: #fff; padding: 15px; border: 1px solid #e2e8f0; border-radius: 15px;'>
                                    <p style='margin: 0 0 10px; color: #64748b; font-size: 13px;'>Mã xác nhận (QR Code)</p>
                                    <img src='{qrUrl}' alt='QR Code' style='width: 150px; height: 150px;'>
                                    <p style='margin: 10px 0 0; font-family: monospace; font-size: 18px; font-weight: bold; color: #0f172a; letter-spacing: 2px;'>{paymentCode}</p>
                                </div>
                                <p style='color: #94a3b8; font-size: 12px; margin-top: 15px;'>Vui lòng xuất trình mã này tại quầy để nhận vé giấy.</p>
                            </div>

                        </div>

                        <!-- Footer -->
                        <div style='background: #f1f5f9; padding: 20px; text-align: center; color: #64748b; font-size: 12px;'>
                            <p style='margin: 0;'>Đây là email xác nhận tự động từ hệ thống Cinema.</p>
                            <p style='margin: 5px 0 0;'>&copy; 2024 Cinema Booking. Mọi quyền được bảo lưu.</p>
                        </div>

                    </div>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}

