using Amazon.SQS;
using Amazon.SQS.Model;
using FCG.Payments.Application.Interfaces;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FCG.Payments.Infra.Messaging;

public sealed class SqsMessageBus : IMessageBus
{
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<SqsMessageBus> _logger;

    public SqsMessageBus(IAmazonSQS sqs, ILogger<SqsMessageBus> logger)
    {
        _sqs = sqs;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string queueUrl, T message, CancellationToken ct = default)
    {
        _logger.LogInformation("[SQS] Starting publish to {QueueUrl}", queueUrl);
        
        try
        {
            var body = JsonSerializer.Serialize(message);
            _logger.LogInformation("[SQS] Message serialized, size: {Size} bytes", body.Length);
            
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = body
            };
            
            _logger.LogInformation("[SQS] Calling SendMessageAsync...");
            var response = await _sqs.SendMessageAsync(request, ct);
            
            _logger.LogInformation("[SQS] SUCCESS! MessageId: {MessageId}", response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SQS] FAILED to publish message");
            throw;
        }
    }
}
