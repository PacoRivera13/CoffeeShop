using System.Collections.Concurrent;
using System.Reflection;
using CoffeeShop.Application.Messaging;
using CoffeeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Infrastructure.Messaging;

/// <summary>
/// Delivers a message to every registered handler in this process. Each handler runs in its own scope and
/// transaction, guarded by a ProcessedMessage row, so a redelivered message never runs a handler twice.
/// </summary>
internal sealed class InProcessMessageBus(IServiceScopeFactory scopes, TimeProvider clock) : IMessageBus
{
    private static readonly MethodInfo DeliverMethod =
        typeof(InProcessMessageBus).GetMethod(nameof(DeliverAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly ConcurrentDictionary<Type, MethodInfo> DeliverByType = new();

    public Task PublishAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        var message = MessageTypes.Deserialize(envelope);
        var deliver = DeliverByType.GetOrAdd(message.GetType(), type => DeliverMethod.MakeGenericMethod(type));

        return (Task)deliver.Invoke(this, [message, new MessageContext(envelope.MessageId), ct])!;
    }

    // Registrations are fixed once the container is built, so the handler types per message type are looked up once.
    private readonly ConcurrentDictionary<Type, Type[]> _handlerTypes = new();

    private async Task DeliverAsync<TMessage>(TMessage message, MessageContext context, CancellationToken ct)
    {
        var handlerTypes = _handlerTypes.GetOrAdd(typeof(TMessage), _ =>
        {
            using var scope = scopes.CreateScope();
            return scope.ServiceProvider.GetServices<IMessageHandler<TMessage>>().Select(h => h.GetType()).ToArray();
        });

        foreach (var handlerType in handlerTypes)
            await InvokeOnceAsync(handlerType, message, context, ct);
    }

    private async Task InvokeOnceAsync<TMessage>(Type handlerType, TMessage message, MessageContext context, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IMessageHandler<TMessage>>().First(h => h.GetType() == handlerType);
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var handlerName = handlerType.FullName!;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == context.MessageId && p.HandlerName == handlerName, ct))
                return;

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // The marker goes in first: its key lock makes a concurrent duplicate wait, and then fail, instead of
            // running the handler a second time.
            db.ProcessedMessages.Add(new ProcessedMessage(context.MessageId, handlerName, clock.GetUtcNow()));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                return;
            }

            await handler.HandleAsync(message, context, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
