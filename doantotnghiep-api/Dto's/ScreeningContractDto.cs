using System.ComponentModel.DataAnnotations;
using doantotnghiep_api.Dto_s;

namespace doantotnghiep_api.DTOs
{
    // Request DTO - map từ form UI
    public class CreateContractRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn phim")]
        public int MovieId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nhà sản xuất")]
        public int ProducerId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Tổng suất chiếu phải lớn hơn 0")]
        public int TotalSlots { get; set; }

        // Tỷ lệ giờ vàng (18:00-22:00) - từ field "Tỷ lệ giờ vàng mong muốn (%)"
        [Range(0, 100, ErrorMessage = "Tỷ lệ giờ vàng phải từ 0-100%")]
        public int GoldHourPercentage { get; set; } = 30;

        // null = áp dụng tất cả rạp
        public int? CinemaId { get; set; }
        public List<TheaterSlotAllocation> TheaterAllocations { get; set; }

    }

    public class TheaterSlotAllocation
    {
        public int TheaterId { get; set; }
        public int AllocatedSlots { get; set; }
    }

    // Response DTO trả về sau khi tạo
    public class ContractResponse
    {
        public int ContractId { get; set; }
        public string MovieTitle { get; set; } = string.Empty;
        public string ProducerName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalSlots { get; set; }
        public int GoldHourSlots { get; set; }      // Suất giờ vàng được tính
        public int RegularSlots { get; set; }       // Suất thường
        public int DurationDays { get; set; }       // Số ngày hợp đồng
        public double AverageSlotsPerDay { get; set; } // TB suất/ngày (hiển thị UI)
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<TheaterSlotBreakdown> TheaterBreakdowns { get; set; } = new();

    }

    public class CreateMovieAndContractRequest
    {
        // --- PHẦN 1: THÔNG TIN PHIM (Để insert vào bảng Movies) ---
        [Required(ErrorMessage = "Tên phim không được để trống")]
        public string MovieTitle { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thời lượng phim")]
        [Range(1, 500, ErrorMessage = "Thời lượng không hợp lệ")]
        public int Duration { get; set; }

        [Required(ErrorMessage = "Thể loại phim không được để trống")]
        public string Genre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng cung cấp ảnh Poster")]
        public string PosterUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày khởi chiếu")]
        public DateTime ReleaseDate { get; set; }

        public string Director { get; set; } = "TBA";
        public string Actors { get; set; } = "TBA";
        public string TrailerUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn giới hạn độ tuổi")]
        public string AgeRating { get; set; } = "P";

        [Required(ErrorMessage = "Vui lòng chọn ngôn ngữ")]
        public string Language { get; set; } = "Vietnamese";


        // --- PHẦN 2: THÔNG TIN HỢP ĐỒNG (Để insert vào bảng ScreeningContracts) ---
        [Required(ErrorMessage = "Vui lòng chọn Nhà sản xuất")]
        public int ProducerId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu hợp đồng")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc hợp đồng")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tổng suất chiếu cam kết")]
        [Range(1, int.MaxValue, ErrorMessage = "Tổng suất chiếu phải lớn hơn 0")]
        public int TotalSlots { get; set; }

        [Range(0, 100, ErrorMessage = "Tỷ lệ giờ vàng phải từ 0-100%")]
        public int GoldHourPercentage { get; set; } = 30;
    }

    // DTO cho danh sách hợp đồng (tiến độ điều phối)
    public class ContractProgressResponse : ContractResponse
    {
        public int UsedSlots { get; set; }          // Suất đã lên lịch
        public int RemainingSlots { get; set; }     // Suất còn lại
        public double ProgressPercent { get; set; } // % tiến độ
        public bool IsBehindSchedule { get; set; }  // Có chậm tiến độ không
        public int SlotsNeededPerDayToComplete { get; set; } // Cần thêm X suất/ngày
    }


}