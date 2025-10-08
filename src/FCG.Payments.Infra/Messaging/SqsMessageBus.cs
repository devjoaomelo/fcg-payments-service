using Amazon.SQS;
using Amazon.SQS.Model;
using FCG.Payments.Application.Interfaces;
using System.Text.Json;

namespace FCG.Payments.Infra.Messaging;

public sealed class SqsMessageBus(IAmazonSQS sqs) : IMessageBus
{
    public async Task PublishAsync<T>(string queueUrl, T message, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(message);
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = body
        }, ct);
    }
}