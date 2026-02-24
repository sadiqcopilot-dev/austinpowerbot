using AustinXPowerBot.Api.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AustinXPowerBot.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<long>, long>(options)
{
    public DbSet<User> LegacyUsers => Set<User>();
    public DbSet<DeviceBinding> DeviceBindings => Set<DeviceBinding>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Signal> Signals => Set<Signal>();
    public DbSet<TradeLog> TradeLogs => Set<TradeLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's ApplicationUser mapping
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<DeviceBinding>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => new { x.DeviceIdHash, x.TelegramId }).IsUnique();
            entity.Property(x => x.DeviceIdHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DeviceModel).HasMaxLength(256).IsRequired();
            entity.HasOne(x => x.User)
                .WithOne(x => x.DeviceBinding)
                .HasForeignKey<DeviceBinding>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<License>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.PlanName).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.User)
                .WithOne(x => x.License)
                .HasForeignKey<License>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Signal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Pair).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Expiry).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<TradeLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Pair).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Expiry).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("numeric(18,6)");
            entity.Property(x => x.Profit).HasColumnType("numeric(18,6)");
            entity.HasOne(x => x.User)
                .WithMany(x => x.TradeLogs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
