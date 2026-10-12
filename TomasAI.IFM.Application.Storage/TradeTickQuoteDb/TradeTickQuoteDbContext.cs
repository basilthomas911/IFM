using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Owns direct ScyllaDB persistence for Databento trades and bounded quote segments.</summary>
public sealed class TradeTickQuoteDbContext(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : ObjectDataRepository<TradeTickQuoteDbContext>(settings[TradeTickQuoteDbConnection], logger), ITradeTickQuoteDbContext
{
    public const string TradeTickQuoteDbConnection = "TradeTickQuoteDbConnection";
    public override TradeTickQuoteDbContext Database => this;
    public ITradeTickQuoteDbReadContext DbReader => this;
    public ITradeTickQuoteDbWriteContext DbWriter => this;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var id = e.TickDataId;
        var data = e.TradeData;
        return this.Use($"{nameof(TradeTickQuoteDbCql)}.{nameof(TradeTickQuoteDbCql.InsertTickTradeData)}", TradeTickQuoteDbCql.InsertTickTradeData)
            .SetParameters(new InsertTickTradeData([(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (long)data.SourceSequence, data.EventTimestampNanoseconds, data.ReceiveTimestampNanoseconds, (short)data.HeaderFlags, data.PriceRaw, data.Price, (long)data.Size, (short)data.Action, (short)data.Side, (short)data.DbnFlags]))
            .ExecuteCommandAsync();
    }

    /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "InsertTickQuoteData transfers the encoded value through IScyllaOwnedBindValues.BindLifetime; the Scylla executor disposes it after binding/execution, including failure.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var id = e.TickDataId;
        var encoded = new TickQuoteScyllaBindValue(e.QuoteData);
        object?[] values = [(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (short)e.EmissionReason, (short)e.QuoteCount, encoded];
        return this.Use($"{nameof(TradeTickQuoteDbCql)}.{nameof(TradeTickQuoteDbCql.InsertTickQuoteData)}", TradeTickQuoteDbCql.InsertTickQuoteData)
            .SetParameters(new InsertTickQuoteData(values, encoded))
            .ExecuteCommandAsync();
    }


    internal static FuturesTickDataV2ReadModel MapToFuturesTickData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), price: e.GetDecimal(4), size: e.GetInt(5));

    internal static FuturesTickDataId MapToFuturesTickDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), TickId: e.GetLong(2));

    internal static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(contractId: e.GetString(0), valueDate: e.GetDateOnly(1), tickId: e.GetLong(2), tickTime: e.GetTimeOnly(3), optionPrice: e.GetDouble(4), bidPrice: e.GetDouble(5), askPrice: e.GetDouble(6), bidSize: e.GetInt(7), askSize: e.GetInt(8), impliedVolatility: e.GetDouble(9), underlyingPrice: e.GetDouble(10), delta: e.GetDouble(11), gamma: e.GetDouble(12), vega: e.GetDouble(13), theta: e.GetDouble(14), rho: e.GetDouble(15));

    internal static FuturesOptionTickDataId MapToFuturesOptionTickDataId<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), TickId: e.GetLong(2));

    internal static FuturesTickHLVDataReadModel MapToFuturesTickHLVData<TDataRecord>(TDataRecord e)
        where TDataRecord : IObjectDataRecord => new(ContractId: e.GetString(0), ValueDate: e.GetDateOnly(1), HighPrice: e.GetDecimal(2), LowPrice: e.GetDecimal(3), Volume: e.GetLong(4));

    /// <inheritdoc />
    public Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId id)
    {
        this.ValidateTickIdentity(id);
        return this.Use(nameof(TradeTickQuoteDbCql.GetFuturesTickData), TradeTickQuoteDbCql.GetFuturesTickData)
            .SetParameters(new TickIdentityParameters(id.ContractId, id.ValueDate, id.TickId))
            .ExecuteSingleAsync(MapToFuturesTickData!);
    }

    /// <inheritdoc />
    public Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.GetLastFuturesTickData), TradeTickQuoteDbCql.GetLastFuturesTickData)
            .SetParameters(new TickContractDateParameters(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesTickData!);
    }

    /// <inheritdoc />
    public Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.GetLastFuturesTickDataId), TradeTickQuoteDbCql.GetLastFuturesTickDataId)
            .SetParameters(new TickContractDateParameters(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesTickDataId!);
    }

    /// <inheritdoc />
    public Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.GetLastFuturesOptionTickDataId), TradeTickQuoteDbCql.GetLastFuturesOptionTickDataId)
            .SetParameters(new TickContractDateParameters(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesOptionTickDataId!);
    }

    /// <inheritdoc />
    public Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId id)
    {
        this.ValidateOptionTickIdentity(id);
        return this.Use(nameof(TradeTickQuoteDbCql.GetFuturesOptionTickData), TradeTickQuoteDbCql.GetFuturesOptionTickData)
            .SetParameters(new TickIdentityParameters(id.ContractId, id.ValueDate, id.TickId))
            .ExecuteSingleAsync(MapToFuturesOptionTickData!);
    }

    /// <inheritdoc />
    public Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.GetLastFuturesOptionTickData), TradeTickQuoteDbCql.GetLastFuturesOptionTickData)
            .SetParameters(new TickContractDateParameters(contractId, valueDate))
            .ExecuteSingleAsync(MapToFuturesOptionTickData!);
    }

    /// <inheritdoc />
    public Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId id)
    {
        this.ValidateFuturesIdentity(id);
        return this.Use(nameof(TradeTickQuoteDbCql.GetFuturesTickHLVData), TradeTickQuoteDbCql.GetFuturesTickHLVData)
            .SetParameters(new TickContractDateParameters(id.ContractId, id.ValueDate))
            .ExecuteSingleAsync(MapToFuturesTickHLVData!);
    }

    /// <inheritdoc />
    public Task<FuturesTickDataV2ReadModel?> GetFuturesTickAtOrBeforeAsync(string contractId, DateOnly valueDate, TimeOnly tickTime, CancellationToken token = default)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.GetFuturesTickAtOrBefore), TradeTickQuoteDbCql.GetFuturesTickAtOrBefore)
            .SetParameters(new TickTimeParameters(contractId, valueDate, tickTime))
            .ExecuteSingleAsync(MapToFuturesTickData!, token);
    }

    /// <inheritdoc />
    public Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel value)
    {
        this.ValidateTick(value);
        return InsertFuturesTickDataAsync(new[] { value });
    }

    /// <inheritdoc />
    public async Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> values)
    {
        this.ValidateTicks(values);
        foreach (var partition in values.GroupBy(row => (row.ContractId, row.ValueDate)))
        {
            foreach (var batch in partition.Chunk(24))
            {
                List<object> commands =
                [
                    this.Use(nameof(TradeTickQuoteDbCql.InsertFuturesTickData), TradeTickQuoteDbCql.InsertFuturesTickData)
                        .SetParameters(batch.Select(row => new InsertFuturesTickData(row.ContractId, row.ValueDate, row.TickId, row.TickTime, row.Price, row.Size)))
                        .QueueCommand(),
                    this.Use(nameof(TradeTickQuoteDbCql.InsertFuturesTickDataByTime), TradeTickQuoteDbCql.InsertFuturesTickDataByTime)
                        .SetParameters(batch.Select(row => new InsertFuturesTickDataByTime(row.ContractId, row.ValueDate, row.TickTime, row.TickId, row.Price, row.Size)))
                        .QueueCommand()
                ];
                await this.ExecuteQueuedCommandsAsync(commands, useTransaction: true).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel value)
    {
        this.ValidateOptionTick(value);
        return InsertFuturesOptionTickDataAsync(new[] { value });
    }

    /// <inheritdoc />
    public Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> values)
    {
        this.ValidateOptionTicks(values);
        return this.Use(nameof(TradeTickQuoteDbCql.InsertFuturesOptionTickData), TradeTickQuoteDbCql.InsertFuturesOptionTickData)
            .SetParameters(values.Select(row => new InsertFuturesOptionTickData(row.ContractId, row.ValueDate, row.TickId, row.TickTime, row.OptionPrice, row.BidPrice, row.AskPrice, row.BidSize, row.AskSize, row.ImpliedVolatility, row.UnderlyingPrice, row.Delta, row.Gamma, row.Vega, row.Theta, row.Rho)))
            .ExecuteCommandAsync();
    }

    /// <inheritdoc />
    public Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        var parameters = new TickContractDateParameters(contractId, valueDate);
        List<object> commands =
        [
            this.Use(nameof(TradeTickQuoteDbCql.DeleteFuturesTickData), TradeTickQuoteDbCql.DeleteFuturesTickData).SetParameters(parameters).QueueCommand(),
            this.Use(nameof(TradeTickQuoteDbCql.DeleteFuturesTickDataByTime), TradeTickQuoteDbCql.DeleteFuturesTickDataByTime).SetParameters(parameters).QueueCommand()
        ];
        return this.ExecuteQueuedCommandsAsync(commands, useTransaction: true);
    }

    /// <inheritdoc />
    public Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.DeleteFuturesOptionTickData), TradeTickQuoteDbCql.DeleteFuturesOptionTickData)
            .SetParameters(new TickContractDateParameters(contractId, valueDate)).ExecuteCommandAsync();
    }
}
