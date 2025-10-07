using FCG.Payments.Domain.Entities;

namespace FCG.Payments.Domain.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> ListAsync(int page, int size, CancellationToken ct = default);
}
