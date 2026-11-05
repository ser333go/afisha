using Afisha.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Afisha.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Venue>(venue =>
        {
            venue.ToTable("Venues");
            venue.Property(v => v.Name).IsRequired().HasMaxLength(120);
            venue.Property(v => v.Address).IsRequired().HasMaxLength(200);
            venue.Property(v => v.City).IsRequired().HasMaxLength(80);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.Property(u => u.Email).IsRequired().HasMaxLength(254);
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);
            user.Property(u => u.Role).IsRequired().HasMaxLength(20).HasDefaultValue("User");
            user.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);

            // SQLite не умеет decimal: храним цену в целых копейках, чтобы сортировка по цене
            // выполнялась базой корректно (целочисленное сравнение, а не сравнение строк).
            entity.Property(e => e.Price).HasConversion<long>(
                rubles => (long)Math.Round(rubles * 100m, MidpointRounding.AwayFromZero),
                cents => cents / 100m);

            // SQLite не хранит DateTimeKind, поэтому при чтении явно помечаем время как UTC.
            entity.Property(e => e.StartsAt).HasConversion(
                new ValueConverter<DateTime, DateTime>(
                    utc => utc,
                    stored => DateTime.SpecifyKind(stored, DateTimeKind.Utc)));

            entity.HasOne(e => e.Venue)
                .WithMany(v => v.Events)
                .HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Author)
                .WithMany(u => u.Events)
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.StartsAt);
            entity.HasIndex(e => e.Price);
            entity.HasIndex(e => e.VenueId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<Venue>().HasData(SeedData.Venues);
        modelBuilder.Entity<User>().HasData(SeedData.Users);
        modelBuilder.Entity<Event>().HasData(SeedData.Events);
    }
}
