using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;
using System.Security.Claims;

namespace FCG.Payments.Application.UseCases.Create;

public sealed record CreatePaymentRequest(Guid GameId);
public sealed record CreatePaymentResponse(Guid Id, Guid UserId, Guid GameId, decimal Amount, PaymentStatus Status, DateTime CreatedAtUtc);

public sealed class CreatePaymentHandler(IPaymentRepository paymentRepository, IGamesCatalogClient gamesClient)
{
    public async Task<CreatePaymentResponse> Handle(CreatePaymentRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userIdStr =user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var userId)) throw new InvalidOperationException("Invalid user identity.");

        var price = await gamesClient.GetPriceAsync(req.GameId, ct);
        if (price is null || price <= 0) throw new InvalidOperationException("Game not found or invalid price.");

        var payment = new Payment(userId, req.GameId, price.Value);
        await paymentRepository.AddAsync(payment, ct);

        return new CreatePaymentResponse(payment.Id, payment.UserId, payment.GameId, payment.Amount, payment.Status, payment.CreatedAtUtc);
    }
}