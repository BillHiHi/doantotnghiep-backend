using System.ComponentModel.DataAnnotations;

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
}
