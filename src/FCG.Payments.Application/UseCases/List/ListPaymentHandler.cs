using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Interfaces;

namespace FCG.Payments.Application.UseCases.List;

public sealed record ListPaymentsItem(Guid Id, Guid UserId, Guid GameId, decimal Amount, PaymentStatus Status, DateTime CreatedAtUtc);
public sealed record ListPaymentsResponse(int Page, int Size, int Count, IReadOnlyList<ListPaymentsItem> Items);

public sealed class ListPaymentsHandler(IPaymentRepository paymentRepository)
{
    public async Task<ListPaymentsResponse> Handle(int page, int size, CancellationToken ct = default)
    {
        var items = await paymentRepository.ListAsync(page, size, ct);
        var dto = items.Select(p => new ListPaymentsItem(p.Id, p.UserId, p.GameId, p.Amount, p.Status, p.CreatedAtUtc)).ToList();
        return new ListPaymentsResponse(page <= 0 ? 1 : page, (size <= 0 || size > 100) ? 10 : size, dto.Count, dto);
    }
}