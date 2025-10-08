using FCG.Payments.Domain.Enums;

namespace FCG.Payments.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    private Payment() { }

    public Payment(Guid userId, Guid gameId, decimal amount)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId required");
        if (gameId == Guid.Empty) throw new ArgumentException("GameId required");
        if (amount <= 0) throw new ArgumentException("Amount must be positive");

        UserId = userId;
        GameId = gameId;
        Amount = amount;
    }

    public bool Confirm()
    {
        if(Status == PaymentStatus.Paid)
        {
            return false;
        }

        Status = PaymentStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public void MarkPaid()
    {
        Status = PaymentStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
