namespace FCG.Payments.Application.Interfaces;

public interface IGamesCatalogClient
{
    Task<decimal?> GetPriceAsync(Guid gameId, CancellationToken ct = default);
}