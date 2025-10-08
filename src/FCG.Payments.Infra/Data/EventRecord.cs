namespace FCG.Payments.Infra.Data;

public sealed class EventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }   // PaymentId
    public string Type { get; set; } = default!; // "PaymentRequested" | "PaymentConfirmed"
    public string Data { get; set; } = default!; // JSON
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
