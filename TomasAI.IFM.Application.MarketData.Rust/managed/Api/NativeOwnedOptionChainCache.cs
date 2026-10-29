using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Query-only public cache facade. Rust owns membership, preparation,
/// publication state, freshness, request policy and all provider resources.</summary>
public sealed class NativeOwnedOptionChainCache(NativeMarketDataRuntime runtime):IOptionChainCache
{
    /// <inheritdoc/>
    public OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result=runtime.QueryDatasetOwnerPreparedCache<ReadResult>(Arguments(request,false));
        if(result.Outcome is <0 or >3 || result.Version<0 || result.Reason is null || (result.Outcome==0)!=(result.Snapshot is not null))throw new InvalidDataException("Malformed native prepared-cache result.");
        return new((OptionChainSnapshotOutcome)result.Outcome,result.Reason,result.Snapshot,result.Version);
    }
    /// <inheritdoc/>
    public OptionChainCacheStatus GetStatus(OptionChainCacheScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        // Remaining request fields are inert for a status-only query. No local
        // business time, provider lookup or eligibility decision is introduced.
        var request=new OptionChainSnapshotRequest(scope.ScopeId,scope.GenerationId,default,"Daily",DateTimeOffset.UnixEpoch);
        var result=runtime.QueryDatasetOwnerPreparedCache<StatusResult>(Arguments(request,true));
        if(result.Version<0 || result.Count is <0 or >2048 || result.Reason is null)throw new InvalidDataException("Malformed native prepared-cache status.");
        return new(result.Published,result.Version,result.PublishedAt,result.Until,result.Count,result.Reason);
    }
    static byte[] Arguments(OptionChainSnapshotRequest r,bool status)=>JsonSerializer.SerializeToUtf8Bytes(new{
        status,scope=r.ScopeId??"",generation=r.GenerationId.ToByteArray().Select(b=>(int)b).ToArray(),date=r.ValueDate.DayNumber,horizon=r.Horizon??"",
        deadline=r.DeadlineUtc.UtcTicks-DateTime.UnixEpoch.Ticks,deadline_offset=(long)r.DeadlineUtc.Offset.TotalMinutes,configuration=r.ConfigurationDigest??"",
        strategy=r.StrategyDefinitionId==Guid.Empty?"":r.StrategyDefinitionId.ToString(),strategy_version=r.StrategyDefinitionVersion,root=r.InstrumentRoot??"",
        required=r.RequiredContractIds.IsDefault?null:r.RequiredContractIds.ToArray(),fund_schema=r.FundPortfolioParameterSchema,age=r.MaximumQuoteAgeMilliseconds,skew=r.MaximumQuoteSkewMilliseconds});
    /// <summary>Exact private transfer shape; public DTO construction changes no values.</summary>
    [MessagePackObject] public sealed record ReadResult([property:Key(0)]int Outcome,[property:Key(1)]string Reason,[property:Key(2)]MarketCompositionSnapshot? Snapshot,[property:Key(3)]long Version);
    /// <summary>Exact private transfer shape for resident publication metadata.</summary>
    [MessagePackObject] public sealed record StatusResult([property:Key(0)]bool Published,[property:Key(1)]long Version,[property:Key(2)]DateTimeOffset? PublishedAt,[property:Key(3)]DateTimeOffset? Until,[property:Key(4)]int Count,[property:Key(5)]string Reason);
}
