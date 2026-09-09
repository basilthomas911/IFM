using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

public sealed record LedgerConfigurationCommandServices(ILedgerConfigurationStore Store,
    IPortfolioFinancialDbContext Database,IPortfolioEventStore Sources,IEventProjector<LedgerConfigurationCommandActor> Projector,
    ILogger<LedgerConfigurationCommandActor> Logger,LegacyFinancialWriterFence? WriterFence=null,
    TomasAI.IFM.Application.Storage.FundDb.IFundDbContext? LegacySource=null,FinancialDevelopmentPolicy? DevelopmentPolicy=null);

public static class ConfigureLedger
{
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this ConfigureLedgerCommand request,LedgerConfigurationCommandServices services,CancellationToken token)
    {
        var replay=await services.Database.ReadOperationAsync<LedgerConfigurationCompletedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,token);
        if(replay is not null) return await replay.NotifyAsync(services.Projector,services.Logger);
        FinancialRequestValidation.Demand(request,"LedgerConfigure",DateTime.UtcNow);
        if(request.Body.Action==LedgerConfigurationAction.ReopenPeriod)
            FinancialRequestValidation.Demand(request,"LedgerPeriodReopen",DateTime.UtcNow);
        if(request.Body.Action is LedgerConfigurationAction.CreateBook or LedgerConfigurationAction.RefreshAuthority)
            await request.ValidateAuthoritySourcesAsync(services.Sources,token);
        if(request.Body.Action==LedgerConfigurationAction.QualifyDevelopmentBook)
            await request.PrepareDevelopmentQualificationAsync(services,token);
        var result=await services.Store.ConfigureAsync(request,receipt=>request.Complete(receipt),FinancialCanonicalHash.Compute,token);
        return await result.NotifyAsync(services.Projector,services.Logger);
    }

    /// <summary>Fences only fresh development scopes and verifies legacy absence before entering the financial transaction.</summary>
    public static async Task PrepareDevelopmentQualificationAsync(this ConfigureLedgerCommand request,LedgerConfigurationCommandServices services,CancellationToken token)
    {
        FinancialRequestValidation.Demand(request,"LedgerImport",DateTime.UtcNow);
        if(services.DevelopmentPolicy?.IsDevelopmentEnvironment!=true || services.WriterFence is null || services.LegacySource is null)
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Development qualification services are unavailable.");
        var book=await services.Database.ReadBookAsync(request.PortfolioId,token);
        if(book is not { Environment:"Emulator",MigrationQualified:false } || book.Funds.Any(x=>x.CanSpend) ||
            request.Body.Book is null || FinancialCanonicalHash.Compute(book)!=FinancialCanonicalHash.Compute(request.Body.Book))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Qualification requires the exact unqualified development book.");
        await request.ValidateAuthoritySourcesAsync(services.Sources,token);
        foreach(var fund in book.Funds.OrderBy(x=>x.FundId))
        {
            // AccountingEntityId is stable across expired command attempts; a pending fence is never reassigned to another book.
            await services.WriterFence.FreezeFreshScopeAsync(book.PortfolioId,fund.FundId,book.AccountingEntityId,token);
            if(await services.LegacySource.HasLegacyFinancialStateAsync(fund.FundId,token))
                throw new FinancialOperationException(FinancialReasons.AuthorityDenied,"Legacy source data requires reconciled migration; fresh-scope qualification is prohibited.");
            await services.WriterFence.VerifyEmptySourceAsync(book.PortfolioId,fund.FundId,book.AccountingEntityId,token);
        }
    }

    public static async Task ValidateAuthoritySourcesAsync(this ConfigureLedgerCommand request,IPortfolioEventStore sources,CancellationToken token)
    {
        var book=request.Body.Book??throw new FinancialOperationException(FinancialReasons.InvalidContract,"Book configuration is required.");
        var portfolio=await sources.LoadPortfolioAsync(new(request.PortfolioId),token);
        var funds=new Dictionary<int,TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFundAggregate>();
        var policies=new Dictionary<int,TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFinancialPolicyAggregate>();
        foreach(var fund in book.Funds)
        {
            funds.Add(fund.FundId,await sources.LoadFundAsync(new(request.PortfolioId,fund.FundId),token));
            if(fund.CanSpend && !policies.ContainsKey(fund.Reference.PolicyId))
                policies.Add(fund.Reference.PolicyId,await sources.LoadPolicyAsync(new(request.PortfolioId,fund.Reference.PolicyId),token));
        }
        FinancialAuthorityModel.Validate(book,portfolio,funds,policies,DateTime.UtcNow);
    }

    public static LedgerConfigurationCompletedEvent Complete(this ConfigureLedgerCommand request,LedgerConfigurationReceipt receipt)=>new()
    {
        Id=Guid.NewGuid(),Subject=new(ActorType.Event,ConfigureLedgerCommand.Actor,nameof(LedgerConfigurationCompletedEvent),request.EntityId.Format()),
        EntityId=request.EntityId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,
        CorrelationId=request.CorrelationId,CausationId=request.CausationId,CommittedAtUtc=receipt.CommittedAtUtc,ReceivedOn=receipt.CommittedAtUtc,
        InputHash=request.InputSha256,AggregateId=request.EntityId.Format(),Receipt=receipt
    };
}
