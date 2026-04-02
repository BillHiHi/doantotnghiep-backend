using System.ComponentModel.DataAnnotations;
using doantotnghiep_api.Models;

public class Movie
{
    [Key]
    public int MovieId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Duration { get; set; }

    public string Genre { get; set; } = string.Empty;

    public string PosterUrl { get; set; } = string.Empty;

    public string Status { get; set; } = "NowShowing";

    public DateTime ReleaseDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string Director { get; set; } = string.Empty;      
    public string Actors { get; set; } = string.Empty;      
    
    public string TrailerUrl { get; set; } = string.Empty;

    [MaxLength(10)]
    public string AgeRating { get; set; } = "P";

    [MaxLength(50)]
    public string Language { get; set; } = "Vietnamese";
    
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual ICollection<Showtime>? Showtimes { get; set; }
    public ICollection<TheaterMovie> TheaterMovies { get; set; } = new List<TheaterMovie>();

}
