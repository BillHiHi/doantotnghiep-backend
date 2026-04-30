using doantotnghiep_api.Models;

namespace doantotnghiep_api.Dto_s
{
    public class ContractTheater
    {
        public int ContractId { get; set; }
        public ScreeningContract Contract { get; set; }

        public int TheaterId { get; set; }
        public Theater Theater { get; set; }

        public int AllocatedSlots { get; set; }
    }

}
