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
/// <param name="settings">The named ScyllaDB connection settings.</param>
/// <param name="logger">Provider diagnostics.</param>
public sealed class TradeTickQuoteDbContext(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : ObjectDataRepository<TradeTickQuoteDbContext>(settings[TradeTickQuoteDbConnection], logger), ITradeTickQuoteDbContext
{
    /// <summary>The named ScyllaDB connection setting.</summary>
    public const string TradeTickQuoteDbConnection = "TradeTickQuoteDbConnection";
    /// <summary>The concrete raw tick persistence context.</summary>
    public override TradeTickQuoteDbContext Database => this;
    /// <summary>The typed raw tick query capability.</summary>
    public ITradeTickQuoteDbReadContext DbReader => this;
    /// <summary>The typed ingestion mutation capability.</summary>
    public ITradeTickQuoteDbWriteContext DbWriter => this;
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
public Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e) => this.InsertTickTradeDataAsyncCore(e);

    /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "InsertTickQuoteData transfers the encoded value through IScyllaOwnedBindValues.BindLifetime; the Scylla executor disposes it after binding/execution, including failure.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
public Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e) => this.InsertTickQuoteDataAsyncCore(e);


    /// <inheritdoc />
    public async Task<IReadOnlyList<BootstrapTradeRow>> GetLatestRawFuturesTradesAsync(string contractId, DateOnly throughDate, CancellationToken token = default)
    {
        this.ValidateScope(contractId, throughDate);
        var rows = await this.Use(nameof(TradeTickQuoteDbCql.GetLatestRawFuturesTrades), TradeTickQuoteDbCql.GetLatestRawFuturesTrades)
            .SetParameters(new BootstrapTradeParameters(contractId, throughDate))
            .ExecuteQueryAsync(row => new BootstrapTradeRow(row.GetDateOnly(0), row.GetDecimal(1), row.GetLong(2), row.GetLong(3),
                row.GetLong(4), row.GetLong(5), row.GetShort(6), row.GetShort(7), checked((uint)row.GetLong(8)), checked((ushort)row.GetInt(9))), token).ConfigureAwait(false);
        return rows.ToArray();
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
    public Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel value) => this.InsertFuturesTickDataAsyncCore(value);

    /// <inheritdoc />
    public Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> values) => this.InsertFuturesTickDataAsyncCore(values);

    /// <inheritdoc />
    public Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel value) => this.InsertFuturesOptionTickDataAsyncCore(value);

    /// <inheritdoc />
    public Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> values) => this.InsertFuturesOptionTickDataAsyncCore(values);

    /// <inheritdoc />
    public Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        var parameters = new TickContractDateParameters(contractId, valueDate);
        List<object> commands =
        [
            this.Use(nameof(TradeTickQuoteDbCql.DeleteFuturesTickData), TradeTickQuoteDbCql.DeleteFuturesTickData)
                .SetParameters(parameters)
                .QueueCommand(),
            this.Use(nameof(TradeTickQuoteDbCql.DeleteFuturesTickDataByTime), TradeTickQuoteDbCql.DeleteFuturesTickDataByTime)
                .SetParameters(parameters)
                .QueueCommand()
        ];
        return this.ExecuteQueuedCommandsAsync(commands, useTransaction: true);
    }

    /// <inheritdoc />
    public Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
        this.ValidateScope(contractId, valueDate);
        return this.Use(nameof(TradeTickQuoteDbCql.DeleteFuturesOptionTickData), TradeTickQuoteDbCql.DeleteFuturesOptionTickData)
            .SetParameters(new TickContractDateParameters(contractId, valueDate))
                .ExecuteCommandAsync();
    }
}
