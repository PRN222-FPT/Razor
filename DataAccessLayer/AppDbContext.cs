using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .HasMaxLength(500);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>().HasData(
            new Category { CategoryId = 1, Name = "Electronics", Description = "Electronic devices and accessories" },
            new Category { CategoryId = 2, Name = "Books", Description = "Physical and digital books" },
            new Category { CategoryId = 3, Name = "Clothing", Description = "Apparel and fashion items" });

        modelBuilder.Entity<Product>().HasData(
            new Product { ProductId = 1, Name = "Laptop", Description = "High-performance laptop", Price = 999.99m, StockQuantity = 50, CategoryId = 1 },
            new Product { ProductId = 2, Name = "Smartphone", Description = "Latest smartphone model", Price = 699.99m, StockQuantity = 100, CategoryId = 1 },
            new Product { ProductId = 3, Name = "C# in Depth", Description = "Advanced C# programming book", Price = 49.99m, StockQuantity = 200, CategoryId = 2 },
            new Product { ProductId = 4, Name = "T-Shirt", Description = "Cotton casual t-shirt", Price = 19.99m, StockQuantity = 500, CategoryId = 3 });
    }
}
