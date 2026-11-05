using System.Text.Json;

namespace CoffeeShop.Application.Messaging;

/// <summary>Broker-agnostic wire shape: a stable type name plus a JSON body, as a queue such as SQS would carry it.</summary>
public sealed record MessageEnvelope(Guid MessageId, string Type, string PayloadJson, DateTimeOffset OccurredAt);

public sealed record MessageContext(Guid MessageId);

public interface IMessageBus
{
    Task PublishAsync(MessageEnvelope envelope, CancellationToken ct);
}

/// <summary>Delivery is at-least-once: implementations must tolerate seeing the same MessageId again.</summary>
public interface IMessageHandler<in TMessage>
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken ct);
}

public sealed record OrderCreated(int OrderId, int CustomerId, decimal Total, string Currency, DateTimeOffset OccurredAt);

public sealed record OrderPaid(int OrderId, int CustomerId, decimal Total, string Currency, DateTimeOffset OccurredAt);

public sealed record OrderCancelled(int OrderId, int CustomerId, decimal Total, string Currency, DateTimeOffset OccurredAt);

/// <summary>Maps stable message names to CLR types, so renaming a class never orphans stored messages.</summary>
public static class MessageTypes
{
    private static readonly Dictionary<string, Type> ByName = new()
    {
        ["OrderCreated"] = typeof(OrderCreated),
        ["OrderPaid"] = typeof(OrderPaid),
        ["OrderCancelled"] = typeof(OrderCancelled),
    };

    private static readonly Dictionary<Type, string> ByType = ByName.ToDictionary(p => p.Value, p => p.Key);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string NameOf(Type type) =>
        ByType.TryGetValue(type, out var name) ? name : throw new ArgumentException($"{type.Name} is not a registered message type.");

    public static Type TypeOf(string name) =>
        ByName.TryGetValue(name, out var type) ? type : throw new ArgumentException($"Unknown message type '{name}'.");

    public static string Serialize<TMessage>(TMessage message) where TMessage : notnull =>
        JsonSerializer.Serialize(message, Json);

    public static object Deserialize(MessageEnvelope envelope) =>
        JsonSerializer.Deserialize(envelope.PayloadJson, TypeOf(envelope.Type), Json)
        ?? throw new InvalidOperationException($"Message {envelope.MessageId} has an empty payload.");
}
