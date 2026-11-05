using CoffeeShop.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace CoffeeShop.Infrastructure.Messaging;

/// <summary>Simulates customer emails with a structured log line; no email is sent.</summary>
public sealed class NotificationHandler(ILogger<NotificationHandler> logger)
    : IMessageHandler<OrderCreated>, IMessageHandler<OrderPaid>, IMessageHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCreated message, MessageContext context, CancellationToken ct) =>
        LogEmail(nameof(OrderCreated), message.OrderId, message.CustomerId, context);

    public Task HandleAsync(OrderPaid message, MessageContext context, CancellationToken ct) =>
        LogEmail(nameof(OrderPaid), message.OrderId, message.CustomerId, context);

    public Task HandleAsync(OrderCancelled message, MessageContext context, CancellationToken ct) =>
        LogEmail(nameof(OrderCancelled), message.OrderId, message.CustomerId, context);

    private Task LogEmail(string messageType, int orderId, int customerId, MessageContext context)
    {
        logger.LogInformation("Email sent for {MessageType} of order {OrderId} to customer {CustomerId} (message {MessageId})",
            messageType, orderId, customerId, context.MessageId);
        return Task.CompletedTask;
    }
}
