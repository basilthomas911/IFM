using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;
/// <summary>Copied native browser outcome; contains no resource or lease handle.</summary>
[MessagePack.MessagePackObject]
public sealed record NativeOwnerBrowserResult([property:MessagePack.Key(0)]EvaluatedOptionChainReadModel? Model,
    [property:MessagePack.Key(1)]int ErrorCode,[property:MessagePack.Key(2)]string? Error);
public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Copies query fields. Native control owns acquisition, view expiry,
    /// required-leg union, window policy, capture and dataset recovery.</summary>
    public Task<NativeOwnerBrowserResult> EvaluateDatasetOwnerOptionChainAsync(GetEvaluatedOptionChainQuery query,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BusinessCommandAsync<NativeOwnerBrowserResult>(new{kind="browser",contract=query.UnderlyingContractId,root=query.UnderlyingSymbol,
            expiry=query.ExpiryDate.DayNumber,providers=query.ProviderRoots,owner=query.SubscriptionOwnerId,required=query.RequiredContractIds,
            multiplier=query.StandardDeviationMultiplier,wing=query.SpreadWingWidth.ToString(CultureInfo.InvariantCulture),release=query.ReleaseOnly,
            frozen=query.AllowFrozenEmulatorPreview&&query.FrozenEmulatorPreviewOnly},token,16*1024*1024);
    }
}
