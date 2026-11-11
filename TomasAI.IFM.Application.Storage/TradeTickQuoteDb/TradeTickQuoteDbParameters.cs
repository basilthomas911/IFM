using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;



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

/// <summary>Binds quote parameters through one shared owner even when the value model is copied.</summary>
internal readonly record struct InsertTickQuoteData : IBindValue
{
    readonly SingleUseScyllaQuoteBinding binding;

    internal InsertTickQuoteData(object?[] values, TickQuoteScyllaBindValue? encoded = null)
        => binding = new(values, encoded);

    public object Bind() => binding;
}

/// <summary>Transfers the exact parameter array once and retains its pooled values until driver execution ends.</summary>
internal sealed class SingleUseScyllaQuoteBinding(object?[] values, TickQuoteScyllaBindValue? encoded)
    : TomasAI.IFM.Framework.Storage.ScyllaDb.IScyllaOwnedBindValues
{
    object?[]? pending = values ?? throw new ArgumentNullException(nameof(values));

    public IDisposable? BindLifetime { get; } = encoded;

    public object?[] TakeValues()
        => Interlocked.Exchange(ref pending, null)
            ?? throw new InvalidOperationException("Tick quote parameters have already been bound.");
}

internal readonly record struct BootstrapTradeParameters(string ContractId, DateOnly ThroughDate) : IBindValue
{
    public object Bind() => new object[] { (sbyte)TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures, ContractId, ThroughDate };
}

/// <summary>Binds the provider-neutral raw-trade window in declared query order.</summary>
internal readonly record struct BootstrapTradeWindowParameters(string ContractId, DateOnly ValueDate, TimeOnly FromUtc, TimeOnly ThroughUtc) : IBindValue
{
    public object Bind() => new object[] { (sbyte)TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures, ContractId, ValueDate, FromUtc, ThroughUtc };
}
