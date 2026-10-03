using FlowForge.Application.Notifications.Messages;

namespace FlowForge.Application.Common.Abstractions;

/// <summary>
/// Application-layer abstraction over whatever message broker Infrastructure wires up
/// (MassTransit + RabbitMQ, today). Application code publishes messages without knowing
/// or caring which library or broker delivers them - same pattern as IEmailSender/ISlackNotifier.
/// </summary>
public interface IMessageBus
{
    Task PublishAsync(DeliverNotificationMessage message, CancellationToken ct = default);
}
