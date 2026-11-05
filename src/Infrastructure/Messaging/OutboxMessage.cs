using CoffeeShop.Application.Messaging;

namespace CoffeeShop.Infrastructure.Messaging;

public enum OutboxStatus
{
    Pending = 1,
    Done = 2,
    Failed = 3
}

/// <summary>A message written in the same transaction as the state change it announces.</summary>
public class OutboxMessage
{
    public const int MaxErrorLength = 2000;

    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public OutboxStatus Status { get; private set; }

    public static OutboxMessage For<TMessage>(TMessage message, DateTimeOffset occurredAt) where TMessage : notnull => new()
    {
        Id = Guid.CreateVersion7(occurredAt),
        Type = MessageTypes.NameOf(typeof(TMessage)),
        PayloadJson = MessageTypes.Serialize(message),
        OccurredAt = occurredAt,
        NextAttemptAt = occurredAt,
        Status = OutboxStatus.Pending,
    };

    public MessageEnvelope ToEnvelope() => new(Id, Type, PayloadJson, OccurredAt);
}

/// <summary>Inbox marker: a handler has already applied a message.</summary>
public class ProcessedMessage
{
    private ProcessedMessage() { }

    public ProcessedMessage(Guid messageId, string handlerName, DateTimeOffset processedAt)
    {
        MessageId = messageId;
        HandlerName = handlerName;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }
    public string HandlerName { get; private set; } = null!;
    public DateTimeOffset ProcessedAt { get; private set; }
}
