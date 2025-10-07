using FCG.Payments.Application.Interfaces;
using System.Net.Http.Json;

namespace FCG.Payments.Infra.Clients;

public sealed class GamesCatalogClient(HttpClient http) : IGamesCatalogClient
{
    public async Task<decimal?> GetPriceAsync(Guid gameId, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/games/{gameId}", ct);
        if (!resp.IsSuccessStatusCode) return null;

        var doc = await resp.Content.ReadFromJsonAsync<GameDto>(cancellationToken: ct);
        return doc?.price;
    }
    private sealed record GameDto(Guid id, string title, string description, decimal price);

}