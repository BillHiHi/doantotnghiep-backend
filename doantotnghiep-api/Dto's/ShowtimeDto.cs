namespace doantotnghiep_api.Dtos
{
    public class ShowtimeDto
    {
        public int ShowtimeId { get; set; }

        public int MovieId { get; set; }
        public string MovieTitle { get; set; } = string.Empty;

        public int ScreenId { get; set; }
        public string ScreenName { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public decimal BasePrice { get; set; }
    }
}
