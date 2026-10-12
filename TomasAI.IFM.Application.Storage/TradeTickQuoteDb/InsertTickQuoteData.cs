using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

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

