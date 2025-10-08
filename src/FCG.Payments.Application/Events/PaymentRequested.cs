namespace FCG.Payments.Application.Events;

public sealed record PaymentRequested(Guid PaymentId, Guid UserId, Guid GameId, decimal Amount, DateTime CreatedAtUtc);