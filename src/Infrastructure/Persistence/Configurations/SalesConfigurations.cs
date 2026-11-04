using CoffeeShop.Application.Auth;
using CoffeeShop.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasOne<IdentityUser>().WithOne().HasForeignKey<Customer>(c => c.IdentityUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> builder) =>
        builder.HasData(
            new IdentityRole(AppRoles.Admin)
            {
                Id = "0f6d2c1e-6a3b-4c58-9d17-3b1f0a7e2c01",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "5b7a9e44-1c2d-4f60-8a3e-7d9c2b6f1a11",
            },
            new IdentityRole(AppRoles.Customer)
            {
                Id = "a84e7b92-2d15-4e6f-b0c3-6e2d9f4a1c02",
                NormalizedName = "CUSTOMER",
                ConcurrencyStamp = "c2e8d3f7-4b1a-49d6-9f25-1a6b8e3c7d22",
            });
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_OrderLines_Qty", "[Qty] > 0"));
        builder.Property(l => l.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.HasOne<Sku>().WithMany().HasForeignKey(l => l.SkuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EffectivePrice>().WithMany()
            .HasForeignKey(l => new { l.PriceId, l.SkuId })
            .HasPrincipalKey(p => new { p.Id, p.SkuId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.HasKey(i => i.ProductId);
        builder.Property(i => i.ProductId).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_Inventories_StockG", "[StockG] >= 0"));
        builder.HasOne<Product>().WithOne().HasForeignKey<Inventory>(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
