using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Notifications.Messages;
using MassTransit;

namespace FlowForge.Infrastructure.Services.Messaging;

public class MassTransitMessageBus : IMessageBus
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitMessageBus(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync(DeliverNotificationMessage message, CancellationToken ct = default)
        => _publishEndpoint.Publish(message, ct);
}
