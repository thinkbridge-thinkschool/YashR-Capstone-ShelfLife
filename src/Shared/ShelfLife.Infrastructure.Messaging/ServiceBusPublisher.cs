using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace ShelfLife.Infrastructure.Messaging;

public sealed class ServiceBusPublisher : IMessagePublisher
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusPublisher> _logger;

    public ServiceBusPublisher(ServiceBusClient client, ILogger<ServiceBusPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
        where T : class
    {
        var sender = _client.CreateSender(topicName);
        var body = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = typeof(T).Name
        };

        // Propagate W3C TraceContext so any consumer can link back to this trace.
        // The consumer reads these properties and calls Activity.SetParentId()
        // before starting its own span, stitching API → worker in a single trace.
        var activity = Activity.Current;
        if (activity != null)
        {
            serviceBusMessage.ApplicationProperties["traceparent"] = activity.Id;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
                serviceBusMessage.ApplicationProperties["tracestate"] = activity.TraceStateString;
        }

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
        _logger.LogInformation("Published {MessageType} to {Topic}", typeof(T).Name, topicName);
    }
}
