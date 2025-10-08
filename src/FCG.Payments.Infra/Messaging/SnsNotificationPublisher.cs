using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FCG.Payments.Application.Interfaces;
using System.Text.Json;

namespace FCG.Payments.Infra.Messaging;

public sealed class SnsNotificationPublisher(IAmazonSimpleNotificationService sns) : INotificationPublisher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase // força camelCase no JSON
    };

    public async Task PublishAsync<T>(string topicArn, T message, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(message, JsonOpts);

        var req = new PublishRequest
        {
            TopicArn = topicArn,
            Message = body,
            Subject = "FCG - Payment Confirmed"
        };

        await sns.PublishAsync(req, ct);
    }
}
