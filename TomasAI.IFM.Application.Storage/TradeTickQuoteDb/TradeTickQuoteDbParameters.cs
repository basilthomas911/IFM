using TomasAI.IFM.Framework.Storage;



namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, double optionPrice, double bidPrice, double askPrice, int bidSize, int askSize, double impliedVolatility, double underlyingPrice, double delta, double gamma, double vega, double theta, double rho) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, price, size };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTickDataByTime(string contractId, DateOnly valueDate, TimeOnly tickTime, long tickId, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickTime, tickId, price, size };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TickContractDateParameters(string ContractId, DateOnly ValueDate) : IBindValue
{
    public object Bind() => new object?[] { ContractId, ValueDate };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TickIdentityParameters(string ContractId, DateOnly ValueDate, long TickId) : IBindValue
{
    public object Bind() => new object?[] { ContractId, ValueDate, TickId };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TickTimeParameters(string ContractId, DateOnly ValueDate, TimeOnly TickTime) : IBindValue
{
    public object Bind() => new object?[] { ContractId, ValueDate, TickTime };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTickTradeData(object?[] Values) : IBindValue
{
    public object Bind() => Values;
}

/// <summary>Single-use reference owner for quote parameters and pooled bind lifetime; it must not be copied as a value binding.</summary>
internal sealed class InsertTickQuoteData(
    object?[] values,
    TickQuoteScyllaBindValue? encoded = null)
    : IBindValue, TomasAI.IFM.Framework.Storage.ScyllaDb.IScyllaOwnedBindValues
{
    private object?[]? _values = values ?? throw new ArgumentNullException(nameof(values));

    /// <summary>Returns this one-time Scylla binding owner to the repository context.</summary>
    public object Bind() => this;

    /// <summary>Retains the encoded quote array until the Scylla driver finishes.</summary>
    public IDisposable? BindLifetime { get; } = encoded;

    /// <summary>Transfers the fresh quote insert values to one prepared-statement bind.</summary>
    public object?[] TakeValues()
        => Interlocked.Exchange(ref _values, null)
            ?? throw new InvalidOperationException("Tick quote parameters have already been bound.");
}

/// <summary>Original provider trade fields used by application cache initialization.</summary>
public sealed record BootstrapTradeRow(DateOnly ValueDate, decimal Price, long Size, long Sequence,
    long EventNanoseconds, long ReceiveNanoseconds, short Action, short HeaderFlags, uint InstrumentId, ushort PublisherId);

internal readonly record struct BootstrapTradeParameters(string ContractId, DateOnly ThroughDate) : IBindValue
{
    public object Bind() => new object[] { (sbyte)TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures, ContractId, ThroughDate };
}
