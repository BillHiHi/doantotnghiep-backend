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

        public string MoviePoster { get; set; } = string.Empty;
        public string MovieGenre { get; set; } = string.Empty;
        public int MovieDuration { get; set; }
        public string MovieTrailer { get; set; } = string.Empty;
        public string MovieAgeRating { get; set; } = "P";

        public int TheaterId { get; set; }
        public decimal BasePrice { get; set; }
        public int AvailableSeats { get; set; }
        public int TotalSeats { get; set; }
    }
}
