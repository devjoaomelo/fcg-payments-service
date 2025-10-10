using FCG.Payments.Application.UseCases.Get;

public interface IPaymentReadRepository
{
    Task<GetPaymentResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
