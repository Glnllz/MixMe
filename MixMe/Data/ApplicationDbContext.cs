using Microsoft.EntityFrameworkCore;
using MixMeShared; // Убедись, что это пространство имен совпадает с твоими классами

namespace MixMe.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // Основные таблицы
    public DbSet<UserProfile> Profiles { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Engineer> Engineers { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<MusicService> Services { get; set; }
    public DbSet<PortfolioItem> Portfolios { get; set; }
    public DbSet<Order> Orders { get; set; }

    // Дополнительные таблицы из мобильной версии
    public DbSet<Review> Reviews { get; set; }
    public DbSet<OrderFile> OrderFiles { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<PostLike> PostLikes { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Complaint> Complaints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        
        modelBuilder.Entity<Engineer>()
            .HasMany(e => e.Genres)
            .WithMany();

        modelBuilder.Entity<MusicService>()
            .Property(s => s.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Customer" },
            new Role { Id = 2, Name = "Engineer" },
            new Role { Id = 3, Name = "Admin" }
        );

        // Seed Data для Жанров
        modelBuilder.Entity<Genre>().HasData(
            new Genre { Id = 1, Name = "Pop" },
            new Genre { Id = 2, Name = "Hip-Hop" },
            new Genre { Id = 3, Name = "Rock" },
            new Genre { Id = 4, Name = "EDM" },
            new Genre { Id = 5, Name = "Jazz" },
            new Genre { Id = 6, Name = "Indie" },
            new Genre { Id = 7, Name = "Metal" }
        );

        // Настройка точности для цен (decimal)
        
    }
}