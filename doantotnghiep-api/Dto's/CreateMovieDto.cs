using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Dto_s
{
    public class CreateMovieDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int Duration { get; set; }

        public string Genre { get; set; } = string.Empty;

        public string PosterUrl { get; set; } = string.Empty;

        public string Director { get; set; } = string.Empty;

        public string Actors { get; set; } = string.Empty;

        public string TrailerUrl { get; set; } = string.Empty;

        public string AgeRating { get; set; } = "P";

        public string Language { get; set; } = "Vietnamese";

        public string Status { get; set; } = "NowShowing";
        public DateTime ReleaseDate { get; set; }
    }
}
