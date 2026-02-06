namespace doantotnghiep_api.Models
{
    public class Theater
    {
        public int TheaterId { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";

        public ICollection<Screen> Screens { get; set; } = new List<Screen>();

    }
}
