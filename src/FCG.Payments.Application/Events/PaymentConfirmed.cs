namespace FCG.Payments.Application.Events;

public sealed record PaymentConfirmed(Guid paymentId,Guid userId, Guid gameId, decimal amount, DateTime confirmedAtUtc);