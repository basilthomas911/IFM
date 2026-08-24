using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial class TradeDbContext
{
    public async Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var optionTrades = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrades)}", TradeDbCql.GetOptionTrades)
            .SetParameters(new GetOptionTrades(orderId))
            .ExecuteQueryAsync(MapToOptionTrade!, cancellationToken);

        var sourceTrades = optionTrades.ToArray();
        var hydratedTrades = new OptionTradeReadModel[sourceTrades.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, sourceTrades.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
                hydratedTrades[index] = await FillOptionTradeAsync(sourceTrades[index], token));
        return [.. hydratedTrades.OrderBy(e => e.IsPrimaryTrade)];
    }

    public async Task<OptionTradeReadModel?> GetOptionTradeAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var optionTrade = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTrade)}", TradeDbCql.GetOptionTrade)
            .SetParameters(new GetOptionTrade(orderId, tradeId))
            .ExecuteSingleAsync(MapToOptionTrade!, cancellationToken);
        return optionTrade is null
            ? null
            : await FillOptionTradeAsync(optionTrade, cancellationToken);
    }

    public Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadData)}", TradeDbCql.GetOptionTradeSpreadData)
            .SetParameters(new GetOptionTradeSpreadData(
                orderId,
                tradeId,
                valueDate,
                tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToOptionTradeSpreadData!, cancellationToken);

    public Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionTradeSpreadBarData)}", TradeDbCql.GetOptionTradeSpreadBarData)
            .SetParameters(new GetOptionTradeSpreadBarData(
                orderId,
                tradeId,
                valueDate,
                tradeType.ToStringFast(),
                startDate,
                endDate))
            .ExecuteQueryAsync(MapToOptionTradeSpreadBarsData!, cancellationToken);

    public Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegsWithValueDate(tradeId, valueDate))
            .ExecuteSingleAsync(MapToTradePrice, cancellationToken);

    public async Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradePositions = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
            .SetParameters(new GetTradePositions(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken);
        if (tradePositions.Count == 0)
            return tradePositions;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var valueDates = tradePositions
            .Select(position => position.ValueDate)
            .Distinct()
            .ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, valueDates.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (dateIndex, token) =>
                legDataByDate[dateIndex] = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                    .SetParameters(new GetOptionLegData(orderId, tradeId, valueDates[dateIndex]))
                    .ExecuteQueryAsync(MapToOptionLegData!, token));

        var optionLegs = await optionLegsTask;
        var optionLegById = optionLegs.ToDictionary(
            leg => leg.ContractId,
            StringComparer.Ordinal);
        var legDataByPosition = BuildLegDataByPosition(
            legDataByDate,
            optionLegById,
            requireOptionLeg: false);
        foreach (var position in tradePositions)
        {
            if (legDataByPosition.TryGetValue(PositionKey(position), out var legData))
                position.AddOptionLegData(legData);
        }
        return tradePositions;
    }

    public async Task<TradePositionReadModel?> GetTradePositionAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradePosition = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePosition)}", TradeDbCql.GetTradePosition)
            .SetParameters(new GetTradePosition(
                orderId,
                tradeId,
                valueDate,
                tradeStatus.ToStringFast(),
                daysToExpiry,
                tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePosition!, cancellationToken);
        if (tradePosition is null)
            return null;

        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(orderId, tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var optionLegDataTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(orderId, tradeId, valueDate))
            .ExecuteQueryAsync(MapToOptionLegData!, cancellationToken);
        await Task.WhenAll(optionLegsTask, optionLegDataTask);
        cancellationToken.ThrowIfCancellationRequested();

        var optionLegById = (await optionLegsTask)
            .ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
        var updatedOptionLegData = (await optionLegDataTask)
            .Where(e => e.TradeType == tradeType
                && e.TradeStatus == tradeStatus
                && e.DaysToExpiry == daysToExpiry)
            .Select(old => (new OptionTradeLegDataReadModel(
                orderId: tradePosition.EntityId.OrderId,
                tradeId: tradePosition.EntityId.TradeId,
                tradeType: tradePosition.EntityId.TradeType,
                valueDate: tradePosition.EntityId.ValueDate,
                daysToExpiry: tradePosition.EntityId.DaysToExpiry,
                tradeStatus: tradePosition.EntityId.TradeStatus,
                optionLegId: old.OptionLegId,
                bidPrice: old.BidPrice,
                askPrice: old.AskPrice,
                impliedVolatility: old.ImpliedVolatility,
                delta: old.Delta,
                gamma: old.Gamma,
                theta: old.Theta,
                vega: old.Vega,
                rho: old.Rho,
                createdOn: old.CreatedOn,
                createdBy: old.CreatedBy,
                updatedOn: old.UpdatedOn,
                updatedBy: old.UpdatedBy))
                .SetOptionLeg(optionLegById.GetValueOrDefault(old.OptionLegId)))
            .ToArray();
        tradePosition.AddOptionLegData(updatedOptionLegData);
        return tradePosition;
    }

    public async Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(
        int orderId,
        CancellationToken cancellationToken)
        => [.. (await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeHistory)}", TradeDbCql.GetTradeHistory)
            .SetParameters(new GetTradeHistory(orderId))
            .ExecuteQueryAsync(MapToTradeHistory!, cancellationToken))
            .OrderBy(e => e.ValueDate)];

    public async Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.TradeDb;
        var tradeFills = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFills)}", TradeDbCql.GetTradeFills)
            .SetParameters(new GetTradeFills(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradeFill!, cancellationToken);
        foreach (var tradeFill in tradeFills)
        {
            var tradeFillData = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeFillData)}", TradeDbCql.GetTradeFillData)
                .SetParameters(new GetTradeFillData(
                    orderId,
                    tradeId,
                    tradeFill.FillDate))
                .ExecuteQueryAsync(MapToTradeFillData!, cancellationToken);
            if (tradeFillData.Count > 0)
                tradeFill.AddTradeFillData(tradeFillData);
        }
        return tradeFills;
    }

    public async Task<ICollection<string>> GetOptionLegContractIdsAsync(
        int tradeId,
        CancellationToken cancellationToken)
        => [.. (await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken))
            .Select(e => e.ContractId)];

    public async Task<int> GetTradeQuantityAsync(
        int tradeId,
        CancellationToken cancellationToken)
    {
        var optionLegs = await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegs)}", TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var latestTradeLegs = optionLegs
            .GroupBy(e => e.OrderId)
            .OrderByDescending(e => e.Max(leg => leg.UpdatedOn))
            .FirstOrDefault();
        return latestTradeLegs is not null
            ? latestTradeLegs.Sum(e => e.Quantity) / latestTradeLegs.Count()
            : 0;
    }

    public async Task<TradeLimitReadModel?> GetTradeLimitAsync(
        int tradeId,
        CancellationToken cancellationToken)
    {
        var tradeLimit = await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeLimit)}", TradeDbCql.GetTradeLimit)
            .SetParameters(new GetTradeLimit(tradeId))
            .ExecuteSingleAsync(MapToTradeLimit!, cancellationToken);
        if (tradeLimit is null)
            return null;

        var tradeTypeLimit = await GetTradeTypeLimitAsync(
            tradeLimit.TradeId,
            tradeLimit.TradeType,
            cancellationToken);
        return tradeTypeLimit is not null
            ? tradeLimit with
            {
                MaxLossLimit = tradeTypeLimit.MaxLossLimit,
                MinProfitLimit = tradeTypeLimit.MinProfitLimit,
                MaxProfitLimit = tradeTypeLimit.MaxProfitLimit
            }
            : tradeLimit;
    }

    public Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(
        int tradeId,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimit)}", TradeDbCql.GetTradeTypeLimit)
            .SetParameters(new GetTradeTypeLimit(tradeId, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradeTypeLimit!, cancellationToken);

    public async Task<ICollection<string>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        int daysToExpiry,
        CancellationToken cancellationToken)
        => [.. (await _dbFactory.TradeDb
            .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositionsById)}", TradeDbCql.GetTradePositionsById)
            .SetParameters(new GetTradePositionsById(
                orderId,
                tradeId,
                valueDate,
                tradeStatus.ToStringFast(),
                daysToExpiry))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken))
            .Select(e => e.TradeType.ToStringFast())];

    async Task<OptionTradeReadModel> FillOptionTradeAsync(
        OptionTradeReadModel optionTrade,
        CancellationToken cancellationToken)
    {
        var entityId = optionTrade.EntityId;
        var db = _dbFactory.TradeDb;
        var tradePositionsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
            .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
            .ExecuteQueryAsync(MapToTradePosition!, cancellationToken);
        var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
            .ExecuteQueryAsync(MapToOptionLeg!, cancellationToken);
        var tradeLimitTask = GetTradeLimitAsync(optionTrade.TradeId, cancellationToken);
        var tradeTypeLimitsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
            .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
            .ExecuteQueryAsync(MapToTradeTypeLimit, cancellationToken);
        var tradeFillsTask = GetTradeFillsAsync(
            optionTrade.OrderId,
            optionTrade.TradeId,
            cancellationToken);

        var tradePositions = await tradePositionsTask;
        var valueDates = tradePositions
            .Select(position => position.ValueDate)
            .Distinct()
            .ToArray();
        var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, valueDates.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (dateIndex, token) =>
                legDataByDate[dateIndex] = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                    .SetParameters(new GetOptionLegData(
                        entityId.OrderId,
                        entityId.TradeId,
                        valueDates[dateIndex]))
                    .ExecuteQueryAsync(MapToOptionLegData!, token));

        await Task.WhenAll(
            optionLegsTask,
            tradeLimitTask,
            tradeTypeLimitsTask,
            tradeFillsTask);
        cancellationToken.ThrowIfCancellationRequested();

        var optionLegs = await optionLegsTask;
        var optionLegById = optionLegs.ToDictionary(
            leg => leg.ContractId,
            StringComparer.Ordinal);
        var legDataByPosition = BuildLegDataByPosition(
            legDataByDate,
            optionLegById,
            requireOptionLeg: true);
        foreach (var position in tradePositions)
        {
            if (legDataByPosition.TryGetValue(PositionKey(position), out var legData))
                position.AddOptionLegData(legData);
        }

        return optionTrade
            .AddOptionLegs(optionLegs)
            .AddTradePosition(tradePositions)
            .SetTradeLimit((await tradeLimitTask)!)
            .AddTradeTypeLimits(await tradeTypeLimitsTask)
            .AddTradeFills(await tradeFillsTask);
    }
}
