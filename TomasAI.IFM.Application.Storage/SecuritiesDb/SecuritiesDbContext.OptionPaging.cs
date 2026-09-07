using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

public partial class SecuritiesDbContext
{
    // Tokens belong to this server lifetime. A restart requires a fresh first page.
    static readonly byte[] OptionPageSigningKey = RandomNumberGenerator.GetBytes(32);
    sealed record OptionPageCursor(string Symbol, int PageSize, ProjectionReadStamp Stamp, byte[] State);

    /// <summary>Reads one page from a completed symbol projection without scanning or repairing the base table.</summary>
    public async Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        GetFuturesOptionContractsPageParameter request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = string.IsNullOrEmpty(request.ContinuationToken) ? null : DecodeOptionCursor(request);
        var db = _dbFactory.SecuritiesDb;
        var stamp = await GetProjectionReadStampAsync(db, FuturesOptionContractSymbolProjection,
            request.Symbol, cancellationToken).ConfigureAwait(false);
        if (stamp is null)
            throw new InvalidOperationException("The option contract catalog is not ready. Complete its symbol projection and retry.");
        if (cursor is not null && cursor.Stamp != stamp.Value)
            throw new InvalidOperationException("The option contract catalog changed. Refresh the list to restart paging.");

        var page = await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)}", SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
            .SetParameters(new GetFuturesOptionContractsBySymbol(request.Symbol))
            .ExecutePageAsync(MapToFuturesOptionContract!, request.PageSize, cursor?.State, cancellationToken)
            .ConfigureAwait(false);
        if (!await IsProjectionReadStampCurrentAsync(db, stamp.Value, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("The option contract catalog changed. Refresh the list to restart paging.");
        var token = page.PagingState is null ? null : EncodeOptionCursor(
            new OptionPageCursor(request.Symbol, request.PageSize, stamp.Value, page.PagingState));
        return new FuturesOptionContractPageReadModel(page.Items, token);
    }

    static string EncodeOptionCursor(OptionPageCursor cursor)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(cursor);
        var signature = HMACSHA256.HashData(OptionPageSigningKey, payload);
        return Convert.ToBase64String(payload) + "." + Convert.ToBase64String(signature);
    }

    static OptionPageCursor DecodeOptionCursor(GetFuturesOptionContractsPageParameter request)
    {
        try
        {
            var parts = request.ContinuationToken!.Split('.');
            if (parts.Length != 2) throw new FormatException();
            var payload = Convert.FromBase64String(parts[0]);
            var signature = Convert.FromBase64String(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(OptionPageSigningKey, payload)))
                throw new FormatException();
            var cursor = JsonSerializer.Deserialize<OptionPageCursor>(payload);
            if (cursor is null || cursor.Symbol != request.Symbol || cursor.PageSize != request.PageSize || cursor.State.Length == 0)
                throw new FormatException();
            return cursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new ArgumentException("Invalid or expired contract continuation token. Refresh the list to restart paging.", nameof(request), ex);
        }
    }
}
