using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;

/// <summary>Resolves a VX source contract to the current ES Market Outlook target.</summary>
internal static class MarketOutlookVxTargetResolver
{
    internal static bool TryResolveVxTarget(
        IMarketOutlookSnapshotRealtimeContext context,
        string sourceContractId,
        DateOnly valueDate,
        out MarketOutlookEntityId target)
    {
        target = default!;
        if (!context.MarketDataApi.TryGetOnTheRunFuturesContract("VX", out var vx)
            || !StringComparer.Ordinal.Equals(vx.ContractId, sourceContractId)
            || !context.MarketDataApi.TryGetOnTheRunFuturesContract("ES", out var es))
            return false;

        target = new(es.ContractId, valueDate);
        return true;
    }

}
