using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Interfaces;
using FCG.Payments.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infra.Repositories;

public sealed class MySqlPaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await db.Payments.AddAsync(payment, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        db.Payments.Update(payment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Payment>> ListAsync(int page, int size, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        size = size <= 0 || size > 100 ? 10 : size;
        var skip = (page - 1) * size;

        var list = await db.Payments
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        return list;
    }
}
