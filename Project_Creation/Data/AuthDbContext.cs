// AuthDbContext.cs
using Microsoft.EntityFrameworkCore;
using Project_Creation.DTO;
using Project_Creation.Models.Entities;
using Project_Creation.Models.ViewModels;

namespace Project_Creation.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> InventoryTransactions { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Users> Users { get; set; }
        public DbSet<Admin> Admin { get; set; }
        public DbSet<UsersAdditionInfo> UsersAdditionInfo { get; set; }
        public DbSet<BOBusinessProfile> BOBusinessProfiles { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Supplier2> Supplier2 { get; set; }
        public DbSet<Product2> Products2 { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<InventoryLog> InventoryLogs { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Sale -> SaleItems relationship
            modelBuilder.Entity<Sale>()
                .HasMany(s => s.SaleItems)
                .WithOne(si => si.Sale)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure SaleItem -> Product2 relationship
            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Product)
                .WithMany(p => p.SaleItems)
                .HasForeignKey(si => si.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Instead, if you want to prevent a relationship to Product2, use this:
            modelBuilder.Entity<Sale>()
                .Ignore("ProductId"); // Only if there's a shadow property being created

            // Configure the one-to-one relationship (ONE CONFIGURATION ONLY)
            modelBuilder.Entity<Users>()
                .HasOne(u => u.AdditionalInfo)
                .WithOne(ai => ai.User)
                .HasForeignKey<UsersAdditionInfo>(ai => ai.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure UserId is unique for one-to-one relationship
            modelBuilder.Entity<UsersAdditionInfo>()
                .HasIndex(ui => ui.UserId)
                .IsUnique();

            // Configure the Calendar entity to properly reference Users
            modelBuilder.Entity<Calendar>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product2>()
                .Property(p => p.SellingPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product2>()
                .Property(p => p.PurchasePrice)
                .HasPrecision(18, 2);

            // Configure decimal precision for Sale.TotalAmount
            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Admin>(entity =>
            {
                entity.HasIndex(a => a.Email).IsUnique();
            });

            // Configure decimal precision for SaleItem prices
            modelBuilder.Entity<SaleItem>()
                .Property(si => si.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<SaleItem>()
                .Property(si => si.TotalPrice)
                .HasColumnType("decimal(18,2)");
        }
        public DbSet<Project_Creation.Models.Entities.Leads> Leads { get; set; } = default!;
        public DbSet<Project_Creation.Models.Entities.Campaign> Campaign { get; set; } = default!;
        public DbSet<Project_Creation.Models.Entities.Staff> Staff { get; set; } = default!;
        public DbSet<Project_Creation.Models.Entities.Calendar> Calendar { get; set; } = default!;
    }
}