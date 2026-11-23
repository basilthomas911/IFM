using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
public sealed partial class MarketConditionAssessmentSnapshotProvider
{
    public async ValueTask<MarketConditionAssessmentSnapshot> CaptureOptionObservationAsync(
        MarketConditionAssessmentParameterSet parameters, DateTime at, CancellationToken token)
    {
        var snapshot = await CaptureObservationAsync(parameters,at,token).ConfigureAwait(false);
        return (snapshot with { Observations = [..snapshot.Observations,..CaptureVxCurve(marketData,at)] }).Seal();
    }

    /// <summary>Reads the feed's ordered eligible pair and original trade times; never acquires history or substitutes zero prices.</summary>
    internal static AssessmentObservation[] CaptureVxCurve(IMarketDataApi source, DateTime at)
    {
        var missing = new[] { Missing("VxFront",at),Missing("VxSecond",at) };
        if (!source.TryGetFuturesTermStructureContracts("VX",out var pair) || !pair.IsValid
            || pair.Front.Symbol != "VX" || pair.Front.LastTradeDate < DateOnly.FromDateTime(at)) return missing;
        var frontHealth = source.GetFuturesMarketHealth(pair.Front.ContractId);
        var backHealth = source.GetFuturesMarketHealth(pair.Back.ContractId);
        if (!frontHealth.Running || !frontHealth.Healthy || !backHealth.Running || !backHealth.Healthy
            || !source.TryGetLastTickPrice(pair.Front.ContractId,out var front) || front.Trade is not {LastPrice:>0} f
            || f.NormalizedTradeAction is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear or NormalizedTradeAction.None
            || !source.TryGetLastTickPrice(pair.Back.ContractId,out var second) || second.Trade is not {LastPrice:>0} s
            || s.NormalizedTradeAction is NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear or NormalizedTradeAction.None
            || !source.TryGetFuturesTermStructureContracts("VX",out var after) || !after.IsValid
            || pair.Front.ContractId != after.Front.ContractId || pair.Back.ContractId != after.Back.ContractId) return missing;
        return [Observed("VxFront",f.EventTimestamp,f.ReceiveTimestamp,f.SourceSequence) with {Value=f.LastPrice,Unit="VX points",Reason=pair.Front.ContractId},
            Observed("VxSecond",s.EventTimestamp,s.ReceiveTimestamp,s.SourceSequence) with {Value=s.LastPrice,Unit="VX points",Reason=pair.Back.ContractId}];
    }
}
