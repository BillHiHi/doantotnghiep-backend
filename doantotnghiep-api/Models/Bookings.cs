using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class Bookings
    {
        [Key]
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public int ShowtimeId { get; set; }
        public int SeatId { get; set; }
        public Seat Seat { get; set; }
        public DateTime BookingDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Hoàn thành";
    }
}
