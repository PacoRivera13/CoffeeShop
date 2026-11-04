using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Origin).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Roast).HasMaxLength(50).IsRequired();
        builder.HasMany(p => p.Skus).WithOne().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(SeedData.Products);
    }
}

internal sealed class SkuConfiguration : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Skus_SizeG", "[SizeG] IN (500, 1000)"));
        builder.Property(s => s.Grind).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.ProductId, s.Grind, s.SizeG }).IsUnique();
        builder.HasData(SeedData.Skus);
    }
}

internal sealed class EffectivePriceConfiguration : IEntityTypeConfiguration<EffectivePrice>
{
    private const string EffectivePriceInsertOnlyTrigger = "TR_EffectivePrices_InsertOnly";

    public void Configure(EntityTypeBuilder<EffectivePrice> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_EffectivePrices_Amount", "[Amount] > 0");
            t.HasTrigger(EffectivePriceInsertOnlyTrigger);
        });
        builder.HasAlternateKey(p => new { p.Id, p.SkuId });
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        builder.HasOne<Sku>().WithMany().HasForeignKey(p => p.SkuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.SkuId, p.EffectiveFrom }).IsUnique();
        builder.HasData(SeedData.Prices);
    }
}
