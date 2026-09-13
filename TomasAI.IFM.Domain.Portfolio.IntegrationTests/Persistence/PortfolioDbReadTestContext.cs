using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

/// <summary>Test-only adapter for focused financial fixtures that do not boot the application DI container.</summary>
internal sealed class PortfolioDbReadTestContext(PortfolioFinancialStore financial) : IPortfolioDbReadContext
{
    public Task<T?> ReadOperationAsync<T>(int portfolioId,Guid operationId,string? inputHash=null,CancellationToken token=default)
        where T:class,IFinancialCompletedEvent=>financial.ReadOperationAsync<T>(portfolioId,operationId,inputHash,token);
    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId,CancellationToken token=default)=>financial.ReadBookAsync(portfolioId,token);
    public Task<PortfolioReadModel?> GetPortfolioAsync(int id,CancellationToken token=default)=>Unsupported<PortfolioReadModel?>();
    public Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int id,CancellationToken token=default)=>Unsupported<PortfolioProjectionRevision?>();
    public Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state,int bucket,int after,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<PortfolioReadModel>>();
    public Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int id,int after,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundMandateReadModel>>();
    public Task<FundMandateReadModel?> GetFundAsync(int id,CancellationToken token=default)=>Unsupported<FundMandateReadModel?>();
    public Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int id,CancellationToken token=default)=>Unsupported<PortfolioProjectionRevision?>();
    public Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int id,int year,string horizon,DateTime at,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundMandateReadModel>>();
    public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetSelectionAssignmentsAsync(int p,int f,long version,string horizon,string root,DateTime at,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundTradeTemplateAssignmentReadModel>>();
    public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int p,int f,long version,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundTradeTemplateAssignmentReadModel>>();
    public Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int p,int f,CancellationToken token=default)=>Unsupported<FundAllocationReadModel?>();
    public Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int p,int f,CancellationToken token=default)=>Unsupported<FundRiskEnvelopeReadModel?>();
    public Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int p,int f,DateOnly month,DateTime before,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundOrderProjectionReadModel>>();
    public Task<FundOrderProjectionReadModel?> GetOrderAsync(int id,CancellationToken token=default)=>Unsupported<FundOrderProjectionReadModel?>();
    public Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int id,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundOrderTradeProjectionReadModel>>();
    public Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int id,CancellationToken token=default)=>Unsupported<FundOrderTradeProjectionReadModel?>();
    public Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid id,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>>();
    public Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(int id,long? version=null,CancellationToken token=default)=>Unsupported<PortfolioFinancialPolicyReadModel?>();
    public Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(int id,int size,CancellationToken token=default)=>Unsupported<IReadOnlyList<PortfolioFinancialPolicyReadModel>>();
    public Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(int id,CancellationToken token=default)=>Unsupported<PortfolioFinancialPolicyReadModel?>();
    static Task<T> Unsupported<T>()=>Task.FromException<T>(new NotSupportedException("This focused fixture exposes financial reads only."));
}
