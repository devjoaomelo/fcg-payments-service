namespace FCG.Payments.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync<T>(string topicArn, T message, CancellationToken ct = default);
}