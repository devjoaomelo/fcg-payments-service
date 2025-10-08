namespace FCG.Payments.Application.Interfaces;

public interface IMessageBus
{
    Task PublishAsync<T>(string queueUrl, T message, CancellationToken ct = default);
}