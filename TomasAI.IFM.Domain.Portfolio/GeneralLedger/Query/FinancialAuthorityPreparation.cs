using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

/// <summary>Reads committed authority sources outside the financial transaction and prepares a revision-bound review draft.</summary>
public sealed class FinancialAuthorityPreparation(FinancialAuthorityPreparationStore financial,IPortfolioEventStore sources,IConfigurationDbContext catalog)
{
    public async Task<FinancialRead<FinancialAuthorityDraft>> PrepareAsync(FinancialReadScope scope,PrepareFinancialAuthorityRequest request,CancellationToken token)
    {
        if(scope.PortfolioId<=0 || scope.FundId is not null || string.IsNullOrWhiteSpace(scope.Access?.Principal) ||
            !(scope.Access.Roles.Contains("PortfolioAdministrator") || scope.Access.Roles.Contains("LedgerConfigure") && scope.Access.PortfolioIds.Contains(scope.PortfolioId)))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Portfolio ledger configuration permission is required.");
        var snapshot=await financial.ReadAsync(scope.PortfolioId,token);
        var portfolio=await sources.LoadPortfolioAsync(new(scope.PortfolioId),token);
        var funds=new Dictionary<int,PortfolioFundAggregate>();
        var definitions=new Dictionary<CatalogKey,StoredStrategyCatalogDefinition>();var seen=new HashSet<CatalogKey>();
        foreach(var owned in snapshot.Book.Funds)
        {
            var fund=await sources.LoadFundAsync(new(scope.PortfolioId,owned.FundId),token);funds.Add(owned.FundId,fund);
            foreach(var key in fund.Assignments.Select(x=>x.TradeStrategyFamily?.CatalogDeployment).OfType<CatalogKey>().Distinct())
            {
                if(!seen.Add(key)) continue;
                if(seen.Count>128) throw new FinancialOperationException(FinancialReasons.InvalidContract,"Authority preparation exceeds the deployment bound.");
                if(await catalog.GetStrategyCatalogAsync(key,token) is { } definition) definitions.Add(key,definition);
            }
        }
        var policies=portfolio.Current?.ActivePolicyId is >0
            ?await sources.LoadPolicyAsync(new(scope.PortfolioId,portfolio.Current.ActivePolicyId),token):null;
        var now=DateTime.UtcNow;
        var draft=FinancialAuthorityPreparationModel.Create(snapshot,portfolio,funds,policies,definitions,request.PermitNewSpending,now);
        return new(FinancialReadStatus.Found,draft,snapshot.Revision,now);
    }
}
