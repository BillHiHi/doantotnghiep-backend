namespace doantotnghiep_api.Dto_s
{
    public class ShowtimeSimpleDto
    {
        public int ShowtimeId { get; set; }

        public string Time { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public decimal BasePrice { get; set; }
    }
}
