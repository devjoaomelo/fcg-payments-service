using FCG.Payments.Application.Interfaces;
using FCG.Payments.Infra.Data;
using System.Text.Json;

namespace FCG.Payments.Infra.Messaging;

public sealed class EventStoreEf : IEventStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PaymentsDbContext _db;
    public EventStoreEf(PaymentsDbContext db) => _db = db;

    public async Task AppendAsync(Guid aggregateId, string type, object data, CancellationToken ct = default)
    {
        _db.Events.Add(new EventRecord
        {
            AggregateId = aggregateId,
            Type = type,
            Data = JsonSerializer.Serialize(data, JsonOpts),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}