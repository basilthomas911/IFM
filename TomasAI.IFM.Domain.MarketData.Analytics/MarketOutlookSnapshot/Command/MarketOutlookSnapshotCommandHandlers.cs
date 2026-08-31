using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;

/// <summary>Creates immutable Market Outlook state transitions from validated commands.</summary>
public static class MarketOutlookSnapshotCommandHandlers
{
    /// <summary>Records all eligible component values carried by one source event.</summary>
    public static ServiceResult<GuidResult> Execute(
        this ObserveMarketOutlookComponentCommand command,
        MarketOutlookSnapshotCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);

        var current = state.WorkingState;
        var watermarks = current.SourceWatermarks.ToDictionary(static value => value.ComponentType);
        var next = current.EntityId == command.EntityId
            ? current
            : new MarketOutlookWorkingStateReadModel { EntityId = command.EntityId };
        var changed = false;

        if (command.FuturesRsiSignal is { } rsi
            && MarketOutlookComponentEligibility.IsEligible(command.EntityId, rsi))
            changed |= Accept(MarketOutlookComponentType.Rsi, value => next = next with
            {
                FuturesRsiSignal = rsi
            });
        if (command.FuturesTdiSignal is { } tdi
            && MarketOutlookComponentEligibility.IsEligible(command.EntityId, tdi))
            changed |= Accept(MarketOutlookComponentType.Tdi, value => next = next with
            {
                FuturesTdiSignal = tdi
            });
        if (command.FuturesItiSignal is { } iti
            && MarketOutlookComponentEligibility.IsEligible(command.EntityId, iti))
        {
            var milestoneType = iti.IntrinsicTimeMode switch
            {
                IntrinsicTimeModeType.TrendDirectionChanged => MarketOutlookComponentType.ItiDirection,
                IntrinsicTimeModeType.TrendExtremeChanged => MarketOutlookComponentType.ItiExtreme,
                IntrinsicTimeModeType.TrendReversalChanged => MarketOutlookComponentType.ItiReversal,
                _ => (MarketOutlookComponentType?)null
            };
            if (milestoneType is { } componentType)
            {
                changed |= Accept(componentType, value => next = componentType switch
                {
                    MarketOutlookComponentType.ItiDirection => next with { TrendDirectionChange = iti },
                    MarketOutlookComponentType.ItiExtreme => next with { TrendExtremeChange = iti },
                    MarketOutlookComponentType.ItiReversal => next with { TrendReversalChange = iti },
                    _ => next
                });
            }
            changed |= Accept(MarketOutlookComponentType.ItiLatest,
                value => next = next with { LatestItiTrendSignal = iti });
        }
        if (command.VixFuturesPrice is >= 0.01m and <= 200m)
            changed |= Accept(MarketOutlookComponentType.Vix, value => next = next with
            {
                VixFuturesPrice = command.VixFuturesPrice
            });
        if (command.FuturesEmaSignal is { } ema
            && MarketOutlookComponentEligibility.IsEligibleAtPublicationBoundary(command.EntityId, ema))
            changed |= Accept(MarketOutlookComponentType.Ema, value => next = next with
            {
                FuturesEmaSignal = ema
            });
        if (command.FuturesBbSignal is { } bb
            && MarketOutlookComponentEligibility.IsEligibleAtPublicationBoundary(command.EntityId, bb))
            changed |= Accept(MarketOutlookComponentType.BollingerBand, value => next = next with
            {
                FuturesBbSignal = bb
            });

        if (!changed)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));

        next = next with
        {
            Revision = checked(current.Revision + 1),
            UpdatedOn = command.SourceEventTimestamp,
            SourceWatermarks = [.. watermarks.Values.OrderBy(static value => value.ComponentType)],
            Status = MarketOutlookStateStatus.Collecting
        };
        next = next with
        {
            PublishedSnapshot = CreateSnapshot(
                command.EntityId,
                next,
                checked((current.PublishedSnapshot?.Revision ?? 0) + 1),
                command.SourceEventTimestamp),
            Status = MarketOutlookStateStatus.Published
        };
        var applied = state.Update(new MarketOutlookComponentObservedEvent
        {
            Subject = EventSubject(
                MarketOutlookComponentObservedEvent.Actor,
                MarketOutlookComponentObservedEvent.Verb,
                command.EntityId),
            EntityId = command.EntityId,
            WorkingState = next,
            SourceEventId = command.SourceEventId,
            SourceEventSequence = command.SourceEventSequence,
            SourceEventName = command.SourceEventName
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed("Unable to apply the Market Outlook component event.");

        bool Accept(
            MarketOutlookComponentType componentType,
            Action<MarketOutlookSourceWatermark> apply)
        {
            watermarks.TryGetValue(componentType, out var currentWatermark);
            var incoming = new MarketOutlookSourceWatermark
            {
                ComponentType = componentType,
                SourceEventId = command.SourceEventId,
                SourceEventSequence = command.SourceEventSequence,
                SourceEventTimestamp = command.SourceEventTimestamp
            };
            if (!IsNewer(incoming, currentWatermark))
                return false;
            apply(incoming);
            watermarks[componentType] = incoming;
            return true;
        }
    }

    /// <summary>Publishes a full checkpoint and UI snapshot at the EOD boundary.</summary>
    public static ServiceResult<GuidResult> Execute(
        this PublishMarketOutlookSnapshotCommand command,
        MarketOutlookSnapshotCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);

        var current = state.WorkingState;
        var eodWatermark = current.SourceWatermarks.FirstOrDefault(
            static value => value.ComponentType == MarketOutlookComponentType.Eod);
        var incomingEod = new MarketOutlookSourceWatermark
        {
            ComponentType = MarketOutlookComponentType.Eod,
            SourceEventId = command.SourceEventId,
            SourceEventSequence = command.SourceEventSequence,
            SourceEventTimestamp = command.SourceEventTimestamp
        };
        if (!IsNewer(incomingEod, eodWatermark))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));

        var reconciled = current.EntityId == command.EntityId
            ? current
            : new MarketOutlookWorkingStateReadModel { EntityId = command.EntityId };
        reconciled = reconciled with
        {
            FuturesRsiSignal = command.FuturesRsiSignal is { } rsi
                && MarketOutlookComponentEligibility.IsEligible(command.EntityId, rsi)
                    ? rsi
                    : reconciled.FuturesRsiSignal,
            FuturesTdiSignal = command.FuturesTdiSignal is { } tdi
                && MarketOutlookComponentEligibility.IsEligible(command.EntityId, tdi)
                    ? tdi
                    : reconciled.FuturesTdiSignal,
            TrendDirectionChange = command.FuturesItiSignalData?.TrendDirectionChange
                ?? reconciled.TrendDirectionChange,
            TrendExtremeChange = command.FuturesItiSignalData?.TrendExtremeChange
                ?? reconciled.TrendExtremeChange,
            TrendReversalChange = command.FuturesItiSignalData?.TrendReversalChange
                ?? reconciled.TrendReversalChange,
            LatestItiTrendSignal = command.FuturesItiSignalData?.TrendDirectionChange
                ?? command.FuturesItiSignalData?.TrendExtremeChange
                ?? command.FuturesItiSignalData?.TrendReversalChange
                ?? reconciled.LatestItiTrendSignal,
            VixFuturesPrice = command.VixFuturesPrice > 0
                ? command.VixFuturesPrice
                : reconciled.VixFuturesPrice,
            FuturesEmaSignal = command.FuturesEmaSignal is { } ema
                && MarketOutlookComponentEligibility.IsEligibleAtPublicationBoundary(command.EntityId, ema)
                    ? ema
                    : reconciled.FuturesEmaSignal,
            FuturesBbSignal = command.FuturesBbSignal is { } bb
                && MarketOutlookComponentEligibility.IsEligibleAtPublicationBoundary(command.EntityId, bb)
                    ? bb
                    : reconciled.FuturesBbSignal,
            FuturesEodData = command.FuturesEodData
        };

        var snapshot = CreateSnapshot(
            command.EntityId,
            reconciled,
            checked((current.PublishedSnapshot?.Revision ?? 0) + 1),
            command.SourceEventTimestamp);
        var watermarks = current.SourceWatermarks
            .Where(static value => value.ComponentType != MarketOutlookComponentType.Eod)
            .Append(incomingEod)
            .OrderBy(static value => value.ComponentType)
            .ToArray();
        var next = reconciled with
        {
            Revision = checked(current.Revision + 1),
            UpdatedOn = command.SourceEventTimestamp,
            PublishedSnapshot = snapshot,
            SourceWatermarks = watermarks,
            Status = MarketOutlookStateStatus.Published
        };
        var applied = state.Update(new MarketOutlookSnapshotPublishedEvent
        {
            Subject = EventSubject(
                MarketOutlookSnapshotPublishedEvent.Actor,
                MarketOutlookSnapshotPublishedEvent.Verb,
                command.EntityId),
            EntityId = command.EntityId,
            WorkingState = next,
            MarketOutlook = snapshot,
            SourceEventId = command.SourceEventId
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed("Unable to apply the Market Outlook snapshot event.");
    }

    static FuturesTradeSignalV2ReadModel? ComputeTradeSignal(
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesEodDataV2ReadModel futuresEodData,
        MarketOutlookWorkingStateReadModel state,
        IReadOnlyCollection<string> missingInputs)
    {
        if (!futuresEodData.IsValid)
            return null;
        var tradeSignalCommand = new UpdateFuturesTradeSignalCommand(
            futuresEodData,
            state.FuturesRsiSignal,
            state.FuturesTdiSignal,
            new FuturesItiSignalDataReadModel(
                state.TrendDirectionChange,
                state.TrendExtremeChange,
                state.TrendReversalChange),
            state.VixFuturesPrice,
            FuturesTradeSignalPrerequisites.SignalTimePeriod);
        return tradeSignalCommand.Compute(out FuturesTradeSignalCompute compute)
            ? compute.FuturesTradeSignal
            : null;
    }

    static List<string> MissingInputs(MarketOutlookWorkingStateReadModel state)
    {
        List<string> missing = [];
        if (state.FuturesEodData is not { IsValid: true }) missing.Add("EOD");
        if (state.FuturesRsiSignal is not { IsWarm: true, RSI: >= 0d }) missing.Add("RSI warming");
        if (state.LatestItiTrendSignal is null) missing.Add("ITI trend");
        if (state.VixFuturesPrice <= 0) missing.Add("VX price");
        if (state.FuturesEmaSignal is not { IsWarm: true }) missing.Add("EMA");
        if (state.FuturesBbSignal is not { IsWarm: true }) missing.Add("Bollinger Bands");
        return missing;
    }

    static MarketOutlookSnapshotReadModel CreateSnapshot(
        MarketOutlookEntityId entityId,
        MarketOutlookWorkingStateReadModel state,
        long revision,
        DateTime updatedOn)
    {
        var missingInputs = MissingInputs(state);
        var eod = state.FuturesEodData ?? new();
        return new MarketOutlookSnapshotReadModel(
            entityId.ContractId,
            entityId.ValueDate,
            revision,
            updatedOn,
            eod,
            ComputeTradeSignal(eod, state, missingInputs),
            string.Join(", ", missingInputs),
            state.FuturesRsiSignal,
            state.FuturesTdiSignal,
            state.TrendDirectionChange,
            state.TrendExtremeChange,
            state.TrendReversalChange,
            state.VixFuturesPrice > 0 ? state.VixFuturesPrice : null,
            state.FuturesEmaSignal,
            state.FuturesBbSignal,
            state.LatestItiTrendSignal);
    }

    static bool IsNewer(
        MarketOutlookSourceWatermark incoming,
        MarketOutlookSourceWatermark? current)
    {
        if (current is null)
            return true;
        if (incoming.SourceEventId == current.SourceEventId)
            return false;
        if (incoming.SourceEventSequence > 0 && current.SourceEventSequence > 0)
            return incoming.SourceEventSequence > current.SourceEventSequence;
        return incoming.SourceEventTimestamp > current.SourceEventTimestamp;
    }

    static ActorSubject EventSubject(string actor, string verb, MarketOutlookEntityId entityId)
        => new(ActorType.Event, actor, verb, entityId.Format());
}
