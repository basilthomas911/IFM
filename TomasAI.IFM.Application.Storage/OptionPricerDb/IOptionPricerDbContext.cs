using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>
/// Combines option-pricer repository, read, and write capabilities.
/// </summary>
public interface IOptionPricerDbContext :
    IObjectRepository<OptionPricerDbContext>,
    IOptionPricerDbReadContext,
    IOptionPricerDbWriteContext
{
    /// <summary>Gets the option-pricer read capability.</summary>
    IOptionPricerDbReadContext DbReader { get; }

    /// <summary>Gets the option-pricer write capability.</summary>
    IOptionPricerDbWriteContext DbWriter { get; }
}
