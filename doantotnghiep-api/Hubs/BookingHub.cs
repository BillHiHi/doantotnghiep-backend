using Microsoft.AspNetCore.SignalR;

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
    }
}
