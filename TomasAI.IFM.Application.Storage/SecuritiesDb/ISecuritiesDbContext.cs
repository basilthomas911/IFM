using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Combines the Securities repository, read, and write contracts.</summary>
public interface ISecuritiesDbContext :
    IObjectRepository<SecuritiesDbContext>,
    ISecuritiesDbReadContext,
    ISecuritiesDbWriteContext
{
    /// <summary>Gets the Securities read capability.</summary>
    ISecuritiesDbReadContext DbReader { get; }

    /// <summary>Gets the Securities write capability.</summary>
    ISecuritiesDbWriteContext DbWriter { get; }
}
