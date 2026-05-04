namespace doantotnghiep_api.Dto_s
{
    public class ShowtimeSimpleDto
    {
        public int ShowtimeId { get; set; }

        public string Time { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public decimal BasePrice { get; set; }
        public string? ScreenType { get; set; }
        public string? ScreenName { get; set; }
        public int ScreenId { get; set; }
    }
}
