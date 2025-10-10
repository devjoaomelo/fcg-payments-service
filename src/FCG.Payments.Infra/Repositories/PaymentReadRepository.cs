using FCG.Payments.Application.UseCases.Get;
using FCG.Payments.Infra.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PaymentReadRepository : IPaymentReadRepository
{
    private readonly PaymentsDbContext _db;
    public PaymentReadRepository(PaymentsDbContext db) => _db = db;

    public async Task<GetPaymentResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Payments
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new GetPaymentResponse(
                p.Id,
                p.UserId,
                p.GameId,
                p.Amount,
                p.Status,
                p.CreatedAtUtc,
                p.UpdatedAtUtc
            ))
            .FirstOrDefaultAsync(ct);
    }
}
