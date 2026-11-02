using CoffeeShop.Domain.Entities;
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
    }
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
