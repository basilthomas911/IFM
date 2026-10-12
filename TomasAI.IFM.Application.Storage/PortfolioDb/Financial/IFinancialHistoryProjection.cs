using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;
using Npgsql;
using NpgsqlTypes;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IFinancialHistoryProjection
{
    Task ApplyAsync(IFinancialCompletedEvent completed, CancellationToken token = default);
}
