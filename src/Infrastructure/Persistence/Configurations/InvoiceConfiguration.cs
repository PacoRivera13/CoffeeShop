using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.Property(i => i.StorageKey).HasMaxLength(200).IsRequired();
        builder.Property(i => i.TotalSnapshot).HasPrecision(18, 2);
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        builder.HasIndex(i => i.OrderId).IsUnique();
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasOne<Order>().WithOne().HasForeignKey<Invoice>(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
