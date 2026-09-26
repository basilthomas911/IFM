using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Combines Trade Plan repository, read, and write capabilities.
/// </summary>
public interface ITradePlanDbContext :
    IObjectRepository<TradePlanDbContext>,
    ITradePlanDbReadContext,
    ITradePlanDbWriteContext
{
    /// <summary>Gets the Trade Plan read capability.</summary>
    ITradePlanDbReadContext DbReader { get; }

    /// <summary>Gets the Trade Plan write capability.</summary>
    ITradePlanDbWriteContext DbWriter { get; }
}
