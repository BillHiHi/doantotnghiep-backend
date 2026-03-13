using System.Threading.Tasks;

namespace doantotnghiep_api.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendTicketEmailAsync(string toEmail, string fullName, string phoneNumber, string movieTitle, string posterUrl, string theaterName, string theaterAddress, string screenName, DateTime showtime, DateTime bookingDate, string paymentCode, decimal totalAmount, string seats, string comboDetails);
    }
}


