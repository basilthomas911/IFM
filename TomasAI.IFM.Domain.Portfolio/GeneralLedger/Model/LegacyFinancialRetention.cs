using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

public sealed record LegacyFinancialRetentionRequest(LegacyFinancialInventoryScope Scope,FinancialAccess Access,string Reason);

/// <summary>Development retention of an explicitly mapped historical Fund. New capital belongs to a separate new book.</summary>
public sealed class LegacyFinancialRetention(IFundDbReadContext source,IPortfolioEventStore sources,
    LegacyFinancialWriterFence fence,LegacyFinancialInventoryStore inventoryStore,LegacyFinancialRetentionStore retentionStore,
    FinancialDevelopmentPolicy development)
{
    public async Task<LegacyFinancialInventoryResult> RetainAsync(LegacyFinancialInventoryScope scope,FinancialAccess access,string reason,CancellationToken token=default)
    {
        if(!development.IsDevelopmentEnvironment || scope.ImportMode!="ReadOnlyHistoryWithDevelopmentCapital" ||
            string.IsNullOrWhiteSpace(access.Principal) || string.IsNullOrWhiteSpace(reason) || reason.Length>1024 ||
            !(access.Roles.Contains("PortfolioAdministrator") || access.Roles.Contains("LedgerImport") && access.PortfolioIds?.Contains(scope.DestinationPortfolioId)==true))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Development retention permission and an audit reason are required.");
        var replay=await retentionStore.ReadAsync(scope,access.Principal,reason,token);
        if(replay is not null) return replay;
        var mapped=await sources.LoadFundAsync(new(scope.DestinationPortfolioId,scope.DestinationFundId),token);
        var portfolio=await sources.LoadPortfolioAsync(new(scope.DestinationPortfolioId),token);
        if(portfolio.Current is null || portfolio.IsDeleted || !portfolio.FundIds.Contains(scope.DestinationFundId))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"The historical Fund must belong to the current Portfolio.");
        if(mapped.Current is not { IsLegacyHistory:true,OperatingState:FundOperatingState.Draft } fund || fund.HistoricalSourceFundId!=scope.SourceFundId)
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Retention requires the exact permanent-Draft legacy Fund mapping.");
        if(await source.HasLegacyFinancialRecordsOutsideRangeAsync(scope.SourceFundId,scope.Start,scope.End,token))
            throw new FinancialOperationException(FinancialReasons.InvalidContract,"The retention manifest must cover all original source dates.");
        // Begin validates the complete immutable scope before any writer is fenced.
        await inventoryStore.BeginAsync(scope,token);
        var pending=await fence.FreezeRetainedScopeAsync(scope.DestinationPortfolioId,scope.SourceFundId,scope.InventoryId,token);
        if(pending!=0 || await source.HasPendingLegacyFinancialWritesAsync(scope.SourceFundId,scope.Start,scope.End,token))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Legacy writes are fenced but pending writes/projections require recovery. Retain the original inventory identity.");
        var inventory=await new LegacyFinancialInventory(source,inventoryStore).RunAsync(scope,token);
        if(await source.HasPendingLegacyFinancialWritesAsync(scope.SourceFundId,scope.Start,scope.End,token) ||
            await source.HasLegacyFinancialRecordsOutsideRangeAsync(scope.SourceFundId,scope.Start,scope.End,token))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Legacy source drain changed during inventory.");
        await retentionStore.SealAsync(scope,inventory,mapped.Revision,portfolio.Revision,access.Principal,reason,token);
        return inventory with { State="RetainedReadOnly" };
    }
}
