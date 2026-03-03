using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class Foods
    {
        [Key]
        public int FoodId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; }
    }
}
