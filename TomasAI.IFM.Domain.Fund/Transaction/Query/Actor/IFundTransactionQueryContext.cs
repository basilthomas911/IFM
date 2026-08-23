using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;

/// <summary>
/// Defines the runtime services required by <see cref="FundTransactionQueryActor"/>.
/// </summary>
public interface IFundTransactionQueryContext : IQueryActorContext<FundTransactionQueryActor>
{
    /// <summary>Gets the database-context factory used by Fund transaction queries.</summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>Gets the logger associated with the Fund transaction query actor.</summary>
    ILogger<FundTransactionQueryActor> Logger { get; }
}
