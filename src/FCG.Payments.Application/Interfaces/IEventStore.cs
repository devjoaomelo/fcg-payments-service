namespace FCG.Payments.Application.Interfaces;

public interface IEventStore
{
    Task AppendAsync(
        Guid aggregateId,
        string type,
        object data,
        CancellationToken ct = default);
}