using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.Application.UseCases.Confirm;

public sealed record ConfirmPaymentRequest(Guid PaymentId);

public sealed class ConfirmPaymentHandler(IPaymentRepository paymentRepository)
{
    public async Task<bool> Handle(ConfirmPaymentRequest req, CancellationToken ct = default)
    {
        var payment = await paymentRepository.GetByIdAsync(req.PaymentId, ct);

        if (payment is null) return false;

        payment.MarkPaid();
        await paymentRepository.UpdateAsync(payment, ct);

        return true;
    }
}