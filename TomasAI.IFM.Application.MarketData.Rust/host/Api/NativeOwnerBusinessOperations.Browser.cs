using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
public sealed partial class NativeOwnerBusinessOperations
{
    /// <summary>Copies the native evaluated result into the existing service
    /// envelope. All policy, lifetimes and financial calculations stay in Rust.</summary>
    public async Task<ServiceResult<EvaluatedOptionChainReadModel>> EvaluateOptionChainAsync(GetEvaluatedOptionChainQuery query,CancellationToken token=default)
    {
        var result=await backend.Runtime.EvaluateDatasetOwnerOptionChainAsync(query,token).ConfigureAwait(false);
        return result.Model is{} model?new ServiceOk<EvaluatedOptionChainReadModel>(model):new ServiceFailed<EvaluatedOptionChainReadModel>(result.ErrorCode,result.Error??"Native option chain is unavailable.");
    }
}
