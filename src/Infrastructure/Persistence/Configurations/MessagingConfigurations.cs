using CoffeeShop.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // SQL Server sorts uniqueidentifier by its last bytes, so even Guid v7 ids would insert at random pages;
        // clustering on OccurredAt keeps outbox inserts sequential.
        builder.HasKey(m => m.Id).IsClustered(false);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasIndex(m => m.OccurredAt).IsClustered();
        builder.Property(m => m.Type).HasMaxLength(100).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(m => m.NextAttemptAt).HasFilter("[Status] = 'Pending'");
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.HasKey(p => new { p.MessageId, p.HandlerName });
        builder.Property(p => p.HandlerName).HasMaxLength(200);
    }
}
