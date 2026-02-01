namespace doantotnghiep_api.Dto_s
{
    public class BookingRequestDTO
    {
        public int ShowtimeId { get; set; } //
        public List<int> SeatIds { get; set; } = new(); //
        public decimal TotalAmount { get; set; } //
        public string PaymentMethod { get; set; } = "Stripe"; //
    }
}
