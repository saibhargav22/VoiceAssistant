using Microsoft.EntityFrameworkCore;
using VoiceAssistant.Core.Models;

namespace VoiceAssistant.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<StockEvent> StockEvents => Set<StockEvent>();
    public DbSet<Cupboard> Cupboards => Set<Cupboard>();
    public DbSet<StorageCategory> StorageCategories => Set<StorageCategory>();
    public DbSet<Budget> Budgets => Set<Budget>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Item
        modelBuilder.Entity<Item>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Unit).HasMaxLength(50);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Inventory — one to one with Item
        modelBuilder.Entity<Inventory>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Item)
             .WithOne(x => x.Inventory)
             .HasForeignKey<Inventory>(x => x.ItemId);
        });

        // Bill
        modelBuilder.Entity<Bill>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Store).HasMaxLength(200);
        });

        // BillItem
        modelBuilder.Entity<BillItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Bill)
             .WithMany(x => x.BillItems)
             .HasForeignKey(x => x.BillId);
            e.HasOne(x => x.Item)
             .WithMany(x => x.BillItems)
             .HasForeignKey(x => x.ItemId);
        });

        // StockEvent
        modelBuilder.Entity<StockEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Item)
             .WithMany(x => x.StockEvents)
             .HasForeignKey(x => x.ItemId);
            e.Property(x => x.EventType).HasConversion<string>();
            e.Property(x => x.Source).HasConversion<string>();
        });

        // Cupboard
        modelBuilder.Entity<Cupboard>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).IsRequired().HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Code).IsUnique();
        });

        // StorageCategory
        modelBuilder.Entity<StorageCategory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Number).IsUnique();
        });

        // Budget
        modelBuilder.Entity<Budget>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Category).IsUnique();
        });
    }
}
