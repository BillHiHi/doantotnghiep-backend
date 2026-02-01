namespace doantotnghiep_api.Models
{
    public class Showtime
    {
        public int ShowtimeId { get; set; } //
        public int MovieId { get; set; } //
        public int ScreenId { get; set; } //
        public DateTime StartTime { get; set; } //
        public DateTime EndTime { get; set; } //
        public decimal BasePrice { get; set; } //

        // Navigation properties
        public virtual Movie Movie { get; set; } = null!; //
        public virtual Screen Screen { get; set; } = null!;

    }
}
