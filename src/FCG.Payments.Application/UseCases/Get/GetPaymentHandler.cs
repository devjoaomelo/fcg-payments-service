using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;
using System.Security.Claims;

namespace FCG.Payments.Application.UseCases.Get;

public sealed record GetPaymentResponse(Guid Id, Guid UserId, Guid GameId, decimal Amount, PaymentStatus Status, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed class GetPaymentHandler(IPaymentRepository paymentRepository)
{
    public async Task<GetPaymentResponse?> Handle(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var p = await paymentRepository.GetByIdAsync(id, ct);
        if (p is null) return null;

        var isAdmin = user.IsInRole("Admin");
        var self = Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value, out var uid) && uid == p.UserId;
        if (!(isAdmin || self)) return null; // controller decide 403/404

        return new GetPaymentResponse(p.Id, p.UserId, p.GameId, p.Amount, p.Status, p.CreatedAtUtc, p.UpdatedAtUtc);
    }
}