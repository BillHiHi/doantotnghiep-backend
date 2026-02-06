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
            modelBuilder.Entity<Bookings>()
                .HasOne(b => b.Seat)
                .WithMany()
                .HasForeignKey(b => b.SeatId);
        }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<SeatLock> SeatLocks { get; set; }
        public DbSet<Showtime> Showtimes { get; set; }
        public DbSet<User> Users { get;set; }
        public DbSet<Bookings> Bookings { get; set; }  
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Screen> Screens { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<Theater> Theaters { get; set; }

    }
}
