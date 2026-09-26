using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Combines Portfolio database repository, read, and write capabilities.</summary>
public interface IPortfolioDbContext :
    IObjectRepository<PortfolioDbContext>,
    IPortfolioDbReadContext,
    IPortfolioDbWriteContext
{
    /// <summary>Gets the Portfolio database read capability.</summary>
    IPortfolioDbReadContext DbReader { get; }

    /// <summary>Gets the Portfolio database write capability.</summary>
    IPortfolioDbWriteContext DbWriter { get; }
}
