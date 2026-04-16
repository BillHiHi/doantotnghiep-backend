using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Dto_s
{
    public class CreateProducerRequest
    {
        [Required(ErrorMessage = "Tên nhà sản xuất không được để trống")]
        [MaxLength(200, ErrorMessage = "Tên không được vượt quá 200 ký tự")]
        public string Name { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [MaxLength(15)]
        public string? PhoneNumber { get; set; }
    }

    public class UpdateProducerRequest
    {
        [Required(ErrorMessage = "Tên nhà sản xuất không được để trống")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [MaxLength(15)]
        public string? PhoneNumber { get; set; }
    }

    public class ProducerResponse
    {
        public int ProducerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public int TotalMovies { get; set; }
        public int TotalContracts { get; set; }
    }
}
