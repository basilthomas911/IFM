using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query;

/// <summary>Narrow, read-only storage boundary used by the legacy history query service.</summary>
public interface ILegacyPortfolioHistoryStore
{
    Task<IReadOnlyList<FundReadModel>> GetFundsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FundOrderReadModel>> GetOrdersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FundOrderTradeReadModel>> GetCompositionTradesAsync(CancellationToken cancellationToken);
    Task<OptionTradeReadModel?> GetTradeDbTradeAsync(int orderId, int tradeId, CancellationToken cancellationToken);
}

/// <summary>Production adapter over the explicitly read-only FundLegacyDb boundary and TradeDb reads.</summary>
public sealed class LegacyPortfolioHistoryStore(
    IFundLegacyDbContext legacyFundDb,
    ITradeDbReadContext tradeDb) : ILegacyPortfolioHistoryStore
{
    readonly IFundDbReadContext _legacy = (legacyFundDb ?? throw new ArgumentNullException(nameof(legacyFundDb))).HistoricalQueries;
    readonly ITradeDbReadContext _tradeDb = tradeDb ?? throw new ArgumentNullException(nameof(tradeDb));

    public async Task<IReadOnlyList<FundReadModel>> GetFundsAsync(CancellationToken cancellationToken) =>
        [.. await _legacy.GetFundsAsync(cancellationToken).ConfigureAwait(false)];

    public async Task<IReadOnlyList<FundOrderReadModel>> GetOrdersAsync(CancellationToken cancellationToken) =>
        [.. await _legacy.GetFundOrdersAsync(cancellationToken).ConfigureAwait(false)];

    public async Task<IReadOnlyList<FundOrderTradeReadModel>> GetCompositionTradesAsync(CancellationToken cancellationToken) =>
        [.. await _legacy.GetFundOrderTradesAsync(cancellationToken).ConfigureAwait(false)];

    public Task<OptionTradeReadModel?> GetTradeDbTradeAsync(int orderId, int tradeId, CancellationToken cancellationToken) =>
        _tradeDb.GetOptionTradeAsync(orderId, tradeId, cancellationToken);
}

/// <summary>
/// Produces source-labelled historical DTOs without mutating, translating, or backfilling either bounded context.
/// Canonical Portfolio order queries do not call this service.
/// </summary>
public sealed class LegacyPortfolioHistoryQueryService(
    ILegacyPortfolioHistoryStore store,
    IPortfolioDbReadContext portfolioDb,
    IPortfolioBusinessIdHighWatermark identityHighWatermark)
{
    readonly ILegacyPortfolioHistoryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    readonly IPortfolioDbReadContext _portfolioDb = portfolioDb ?? throw new ArgumentNullException(nameof(portfolioDb));
    readonly IPortfolioBusinessIdHighWatermark _identityHighWatermark = identityHighWatermark ?? throw new ArgumentNullException(nameof(identityHighWatermark));

    public async Task<ServiceResult<LegacyPortfolioScopeReadModel[]>> GetScopesAsync(CancellationToken cancellationToken = default)
    {
        var highWatermark = await _identityHighWatermark.GetPortfolioHighWatermarkAsync(cancellationToken).ConfigureAwait(false);
        if (highWatermark <= 0) return new ServiceOk<LegacyPortfolioScopeReadModel[]>([]);
        var result = new List<LegacyPortfolioScopeReadModel>();
        for (var bucket = 0; bucket <= (highWatermark - 1) / 1000; bucket++)
        {
            var portfolios = await _portfolioDb.GetPortfoliosByStateAsync(
                PortfolioOperatingState.Draft, bucket, bucket * 1000, 200, cancellationToken).ConfigureAwait(false);
            foreach (var portfolio in portfolios)
            {
                var funds = await _portfolioDb.GetFundsByPortfolioAsync(portfolio.PortfolioId, 0, 200, cancellationToken).ConfigureAwait(false);
                var mapped = funds.Where(x => x.IsLegacyHistory).OrderBy(x => x.HistoricalSourceFundId).ToArray();
                if (mapped.Length > 0)
                    result.Add(new LegacyPortfolioScopeReadModel { Portfolio = portfolio.DefensiveCopy(), Funds = mapped });
            }
        }
        return new ServiceOk<LegacyPortfolioScopeReadModel[]>([.. result.OrderBy(x => x.Portfolio.PortfolioId)]);
    }

    public async Task<ServiceResult<LegacyFundHistoryReadModel[]>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var fundsTask = _store.GetFundsAsync(cancellationToken);
        var ordersTask = _store.GetOrdersAsync(cancellationToken);
        var tradesTask = _store.GetCompositionTradesAsync(cancellationToken);
        await Task.WhenAll(fundsTask, ordersTask, tradesTask).ConfigureAwait(false);
        var orders = await ordersTask;
        var trades = await tradesTask;
        var sourceFunds = (await fundsTask)
            .Where(x => x.FundId > 0)
            .OrderBy(x => x.FundId)
            .ToArray();
        var rows = sourceFunds
            .Select(x => new LegacyFundHistoryReadModel
            {
                Fund = x,
                OrderCount = orders.Count(y => y.FundId == x.FundId),
                CompositionTradeCount = trades.Count(y => y.FundId == x.FundId),
            }).ToList();
        var knownFundIds = sourceFunds.Select(x => x.FundId).ToHashSet();
        var orphanFundIds = orders.Select(x => x.FundId)
            .Concat(trades.Select(x => x.FundId))
            .Where(x => !knownFundIds.Contains(x))
            .Distinct()
            .Order()
            .ToArray();
        foreach (var orphanFundId in orphanFundIds)
        {
            var label = orphanFundId > 0
                ? $"Unassigned Legacy Records (source Fund {orphanFundId})"
                : "Unassigned Legacy Records";
            rows.Add(new LegacyFundHistoryReadModel
            {
                Fund = new FundReadModel(orphanFundId, label, "Historical rows whose FundId has no matching legacy Fund definition.", 0m, false, DateTime.UnixEpoch, "legacy-history"),
                OrderCount = orders.Count(x => x.FundId == orphanFundId),
                CompositionTradeCount = trades.Count(x => x.FundId == orphanFundId),
                IsUnassigned = true,
            });
        }
        return new ServiceOk<LegacyFundHistoryReadModel[]>([.. rows]);
    }

    public async Task<ServiceResult<LegacyFundOrderHistoryReadModel[]>> GetOrdersAsync(
        int legacyFundId, DateOnly fromDate, DateOnly toDate, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateFundId(legacyFundId);
            if (fromDate == default || toDate == default || fromDate > toDate) throw new ArgumentException("A valid inclusive legacy order date range is required.");
            if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be 1..1000.");
            var ordersTask = _store.GetOrdersAsync(cancellationToken);
            var tradesTask = _store.GetCompositionTradesAsync(cancellationToken);
            await Task.WhenAll(ordersTask, tradesTask).ConfigureAwait(false);
            var counts = (await tradesTask).Where(x => x.FundId == legacyFundId).GroupBy(x => x.OrderId).ToDictionary(x => x.Key, x => x.Count());
            var rows = (await ordersTask)
                .Where(x => x.FundId == legacyFundId
                    && (x.TradeDate == DateOnly.MinValue || x.TradeDate >= fromDate && x.TradeDate <= toDate))
                .OrderByDescending(x => x.TradeDate).ThenByDescending(x => x.OrderId)
                .Take(pageSize)
                .Select(x => new LegacyFundOrderHistoryReadModel { Order = x, CompositionTradeCount = counts.GetValueOrDefault(x.OrderId) })
                .ToArray();
            return new ServiceOk<LegacyFundOrderHistoryReadModel[]>(rows);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            return new ServiceFailed<LegacyFundOrderHistoryReadModel[]>(PortfolioErrorCodes.ValidationFailed, ex.Message);
        }
    }

    public async Task<ServiceResult<LegacyFundTradeHistoryReadModel[]>> GetOrderTradesAsync(
        int legacyFundId, int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateFundId(legacyFundId);
            if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
            var compositions = (await _store.GetCompositionTradesAsync(cancellationToken).ConfigureAwait(false))
                .Where(x => x.FundId == legacyFundId && x.OrderId == orderId)
                .OrderBy(x => x.TradeId).ToArray();
            var rows = new LegacyFundTradeHistoryReadModel[compositions.Length];
            await Parallel.ForEachAsync(Enumerable.Range(0, compositions.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
                async (index, token) =>
                {
                    var composition = compositions[index];
                    var trade = await _store.GetTradeDbTradeAsync(composition.OrderId, composition.TradeId, token).ConfigureAwait(false);
                    rows[index] = new LegacyFundTradeHistoryReadModel
                    {
                        Composition = composition,
                        TradeDbTrade = trade,
                        MatchStatus = LegacyFundTradeHistoryReadModel.Classify(trade),
                        FillCount = trade?.TradeFills?.Length ?? 0,
                        PositionCount = trade?.TradePositions?.Length ?? 0,
                        OptionLegCount = trade?.OptionLegs?.Length ?? 0,
                    };
                }).ConfigureAwait(false);
            return new ServiceOk<LegacyFundTradeHistoryReadModel[]>(rows);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            return new ServiceFailed<LegacyFundTradeHistoryReadModel[]>(PortfolioErrorCodes.ValidationFailed, ex.Message);
        }
    }

    static void ValidateFundId(int legacyFundId)
    {
        if (legacyFundId < 0) throw new ArgumentOutOfRangeException(nameof(legacyFundId), "Legacy FundId must be zero or positive.");
    }
}
