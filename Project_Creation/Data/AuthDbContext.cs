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
        public DbSet<Supplier> Supplier2 { get; set; }
        public DbSet<Product> Products2 { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<InventoryLog> InventoryLogs { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Calendar> Calendar { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<UserSocialMediaLinks> UserSocialMediaLinks { get; set; }
        public DbSet<LeadActivityLog> LeadActivityLogs { get; set; }
        public DbSet<MessageTemplate> MessageTemplates { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CalendarTask> CalendarTasks { get; set; }
        public DbSet<ProductOrder> ProductOrders { get; set; }
        public DbSet<StaffActivityLogs> StaffActivityLogs { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }

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

            // Configure the Calendar entity to properly reference Users
            modelBuilder.Entity<Staff>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.BOId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .Property(p => p.SellingPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
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

            // Configure ProductOrder entity
            modelBuilder.Entity<ProductOrder>()
                .Property(po => po.UnitPrice)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<ProductOrder>()
                .Property(po => po.TotalPrice)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<ProductOrder>()
                .HasOne(po => po.Buyer)
                .WithMany()
                .HasForeignKey(po => po.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<ProductOrder>()
                .HasOne(po => po.Seller)
                .WithMany()
                .HasForeignKey(po => po.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<ProductOrder>()
                .HasOne(po => po.Product)
                .WithMany()
                .HasForeignKey(po => po.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // leads keys
            modelBuilder.Entity<Leads>()
                .HasOne(l => l.CreatedBy)
                .WithMany()
                .HasForeignKey(l => l.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Leads>()
                .HasOne(l => l.Product)
                .WithMany()
                .HasForeignKey(l => l.LastPurchasedId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Leads>()
                .HasOne(l => l.CreatedBy)
                .WithMany()
                .HasForeignKey(l => l.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure MessageTemplate entity
            modelBuilder.Entity<MessageTemplate>()
                .HasOne(mt => mt.BusinessOwner)
                .WithMany()
                .HasForeignKey(mt => mt.BOId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Name)
                .IsRequired()
                .HasMaxLength(100);
                
            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Subject)
                .IsRequired();
                
            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Content)
                .IsRequired();
        }
        public DbSet<Project_Creation.Models.Entities.Leads> Leads { get; set; } = default!;
        public DbSet<Project_Creation.Models.Entities.Campaign> Campaign { get; set; } = default!;
        public DbSet<Project_Creation.Models.Entities.Staff> Staff { get; set; } = default!;
    }
}