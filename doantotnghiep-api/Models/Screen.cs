namespace doantotnghiep_api.Models
{
public class Screen
{
    public int ScreenId { get; set; }
    public int TheaterId { get; set; }
    public string ScreenName { get; set; } = "";
    public string ScreenType { get; set; } = "";

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
}
