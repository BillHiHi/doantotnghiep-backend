namespace doantotnghiep_api.Models
{
public class Screen
{
    public int ScreenId { get; set; }
    public int TheaterId { get; set; }
    public string ScreenName { get; set; } = "";
    public string ScreenType { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public ICollection<Seat>? Seats { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public ICollection<Showtime>? Showtimes { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Theater? Theater { get; set; } 

    }
}
