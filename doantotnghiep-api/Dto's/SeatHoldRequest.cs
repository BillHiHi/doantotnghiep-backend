using System.Text.Json.Serialization;

namespace doantotnghiep_api.Dto_s
{
    public class SeatHoldRequest
    {
        [JsonPropertyName("seatId")]
        public int SeatId { get; set; }

        [JsonPropertyName("showtimeId")]
        public int ShowtimeId { get; set; }

        [JsonPropertyName("userId")]
        public int UserId { get; set; }
    }

    public class ConfirmAllRequest
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("showtimeId")]
        public int ShowtimeId { get; set; }

        [JsonPropertyName("paymentCode")]
        public string PaymentCode { get; set; } = string.Empty;
    }
}
