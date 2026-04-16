using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace doantotnghiep_api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
    : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình khóa chính cho bảng trung gian
            modelBuilder.Entity<TheaterMovie>()
                .HasKey(tm => new { tm.MovieId, tm.TheaterId });

            modelBuilder.Entity<TheaterMovie>()
                .HasOne(tm => tm.Movie)
                .WithMany(m => m.TheaterMovies)
                .HasForeignKey(tm => tm.MovieId);

            modelBuilder.Entity<TheaterMovie>()
                .HasOne(tm => tm.Theater)
                .WithMany() // Nếu bảng Theater không có ICollection<TheaterMovie> thì để trống
                .HasForeignKey(tm => tm.TheaterId);
        }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<SeatLock> SeatLocks { get; set; }
        public DbSet<Showtime> Showtimes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Bookings> Bookings { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Screen> Screens { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<Theater> Theaters { get; set; }
        public DbSet<Foods> Foods { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<TheaterMovie> TheaterMovies { get; set; }
        public DbSet<PointTransaction> PointTransactions { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<UserVoucher> UserVouchers { get; set; }

        public DbSet<Producer> Producers { get; set; }
        public DbSet<ScreeningContract> ScreeningContracts { get; set; }

    }
}
