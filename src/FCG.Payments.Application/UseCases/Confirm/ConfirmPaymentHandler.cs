using FCG.Payments.Application.Events;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FCG.Payments.Application.UseCases.Confirm;

public sealed record ConfirmPaymentRequest(Guid Id);

public sealed class ConfirmPaymentHandler(
    IPaymentRepository paymentRepository,
    IEventStore eventStore,
    INotificationPublisher notifier,
    IConfiguration cfg)
{
    public async Task<bool> Handle(ConfirmPaymentRequest req, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByIdAsync(req.Id, ct);
        if (payment is null) return false;

        var changed = payment.Confirm();
        if (!changed)
            return true; 

        await paymentRepository.UpdateAsync(payment, ct);

        await eventStore.AppendAsync(
            payment.Id,
            "PaymentConfirmed",
            new
            {
                paymentId = payment.Id,
                userId = payment.UserId,
                gameId = payment.GameId,
                amount = payment.Amount,
                confirmedAtUtc = payment.UpdatedAtUtc
            },
            ct);

        var evt = new PaymentConfirmed(
            paymentId: payment.Id,
            userId: payment.UserId,
            gameId: payment.GameId,
            amount: payment.Amount,
            confirmedAtUtc: DateTime.UtcNow
        );

        var topicArn = cfg["Notifications:TopicArn"]
            ?? throw new InvalidOperationException("Notifications:TopicArn not configured");

        await notifier.PublishAsync(topicArn, evt, ct);
        return true;
    }
}