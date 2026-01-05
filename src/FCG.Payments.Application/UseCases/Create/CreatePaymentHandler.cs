using System.Security.Claims;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.Application.UseCases.Create;

public sealed record CreatePaymentRequest(Guid GameId);
public sealed record CreatePaymentResponse(
    Guid Id, Guid UserId, Guid GameId, decimal Amount, PaymentStatus Status, DateTime CreatedAtUtc);

public sealed class CreatePaymentHandler(
    IPaymentRepository paymentRepository,
    IGamesCatalogClient gamesClient,
    IMessageBus bus,
    IEventStore eventStore)
{
    public async Task<CreatePaymentResponse> Handle(
        CreatePaymentRequest req,
        ClaimsPrincipal user,
        string paymentsRequestedQueueUrl,
        CancellationToken ct = default)
    {

        if (string.IsNullOrWhiteSpace(paymentsRequestedQueueUrl))
            throw new InvalidOperationException("Queue URL not configured (PaymentsRequested).");

        if (!paymentsRequestedQueueUrl.StartsWith("https://sqs."))
            throw new InvalidOperationException(
                $"Invalid SQS queue URL format: {paymentsRequestedQueueUrl}");

        ct.ThrowIfCancellationRequested();

        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new InvalidOperationException("Authenticated user id (sub) is missing or invalid.");

        var price = await gamesClient.GetPriceAsync(req.GameId, ct);
        if (price is null || price <= 0)
            throw new InvalidOperationException("Game not found or invalid price.");

        var payment = new Payment(userId, req.GameId, price.Value);
        await paymentRepository.AddAsync(payment, ct);

        await eventStore.AppendAsync(
            payment.Id,
            "PaymentRequested",
            new
            {
                paymentId = payment.Id,
                userId = payment.UserId,
                gameId = payment.GameId,
                amount = payment.Amount,
                createdAtUtc = payment.CreatedAtUtc
            },
            ct);

        var outgoing = new
        {
            paymentId = payment.Id,
            userId = payment.UserId,
            gameId = payment.GameId,
            amount = payment.Amount,
            createdAtUtc = payment.CreatedAtUtc
        };
        await bus.PublishAsync(paymentsRequestedQueueUrl, outgoing, ct);

        return new CreatePaymentResponse(
            payment.Id, payment.UserId, payment.GameId, payment.Amount, payment.Status, payment.CreatedAtUtc);
    }
}