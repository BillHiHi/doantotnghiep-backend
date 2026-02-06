namespace doantotnghiep_api.Dto_s
{
    public class PaymentRequestDto
    {
        public int UserId { get; set; }
        public int ShowtimeId { get; set; }
        public List<int> SeatIds { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }

    public class PaymentResultDto
    {
        public string QrUrl { get; set; } = "";
        public string PaymentCode { get; set; } = "";
    }

}
