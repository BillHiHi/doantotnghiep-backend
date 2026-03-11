using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace doantotnghiep_api.Hubs
{
    public class BookingHub : Hub
    {
        public async Task JoinShowtimeGroup(int showtimeId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"Showtime_{showtimeId}"
            );
        }

        public async Task LeaveShowtimeGroup(int showtimeId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"Showtime_{showtimeId}"
            );
        }

        // Bắn tín hiệu khi có người bấm giữ ghế
        public async Task SelectSeat(int showtimeId, string seatId)
        {
            // Báo cho các máy KHÁC trong cùng suất chiếu biết ghế này đang được chọn
            await Clients.OthersInGroup($"Showtime_{showtimeId}")
                         .SendAsync("OnSeatSelected", seatId);
        }

        // Bắn tín hiệu khi có người bỏ chọn ghế
        public async Task DeselectSeat(int showtimeId, string seatId)
        {
            // Báo cho các máy KHÁC biết ghế này đã được giải phóng
            await Clients.OthersInGroup($"Showtime_{showtimeId}")
                         .SendAsync("OnSeatDeselected", seatId);
        }
    }
}