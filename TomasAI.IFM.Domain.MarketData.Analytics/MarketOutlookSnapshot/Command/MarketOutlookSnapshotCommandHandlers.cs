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

        if (command.FuturesRsiSignal is { } rsi)
            changed |= Accept(MarketOutlookComponentType.Rsi, value => next = next with
            {
                FuturesRsiSignal = rsi
            });
        if (command.FuturesTdiSignal is { } tdi)
            changed |= Accept(MarketOutlookComponentType.Tdi, value => next = next with
            {
                FuturesTdiSignal = tdi
            });
        if (command.FuturesItiSignal is { } iti)
        {
            var componentType = iti.IntrinsicTimeMode switch
            {
                IntrinsicTimeModeType.TrendDirectionChanged => MarketOutlookComponentType.ItiDirection,
                IntrinsicTimeModeType.TrendExtremeChanged => MarketOutlookComponentType.ItiExtreme,
                IntrinsicTimeModeType.TrendReversalChanged => MarketOutlookComponentType.ItiReversal,
                _ => throw new InvalidOperationException(
                    $"Unsupported Market Outlook ITI mode {iti.IntrinsicTimeMode}.")
            };
            changed |= Accept(componentType, value => next = componentType switch
            {
                MarketOutlookComponentType.ItiDirection => next with { TrendDirectionChange = iti },
                MarketOutlookComponentType.ItiExtreme => next with { TrendExtremeChange = iti },
                MarketOutlookComponentType.ItiReversal => next with { TrendReversalChange = iti },
                _ => next
            });
        }
        if (command.VixFuturesPrice > 0)
            changed |= Accept(MarketOutlookComponentType.Vix, value => next = next with
            {
                VixFuturesPrice = command.VixFuturesPrice
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
        if (next.FuturesEodData is { } eod && current.PublishedSnapshot is not null)
        {
            var missingInputs = MissingInputs(next);
            var tradeSignal = ComputeTradeSignal(eod, next, missingInputs)
                ?? current.PublishedSnapshot.FuturesTradeSignal;
            next = next with
            {
                PublishedSnapshot = new MarketOutlookSnapshotReadModel(
                    command.EntityId.ContractId,
                    command.EntityId.ValueDate,
                    checked(current.PublishedSnapshot.Revision + 1),
                    command.SourceEventTimestamp,
                    eod,
                    tradeSignal,
                    string.Join(", ", missingInputs)),
                Status = MarketOutlookStateStatus.Published
            };
        }
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
            FuturesRsiSignal = command.FuturesRsiSignal ?? reconciled.FuturesRsiSignal,
            FuturesTdiSignal = command.FuturesTdiSignal ?? reconciled.FuturesTdiSignal,
            TrendDirectionChange = command.FuturesItiSignalData?.TrendDirectionChange
                ?? reconciled.TrendDirectionChange,
            TrendExtremeChange = command.FuturesItiSignalData?.TrendExtremeChange
                ?? reconciled.TrendExtremeChange,
            TrendReversalChange = command.FuturesItiSignalData?.TrendReversalChange
                ?? reconciled.TrendReversalChange,
            VixFuturesPrice = command.VixFuturesPrice > 0
                ? command.VixFuturesPrice
                : reconciled.VixFuturesPrice,
            FuturesEodData = command.FuturesEodData
        };

        var missingInputs = MissingInputs(reconciled);
        var tradeSignal = ComputeTradeSignal(command.FuturesEodData, reconciled, missingInputs)
            ?? current.PublishedSnapshot?.FuturesTradeSignal;
        var snapshot = new MarketOutlookSnapshotReadModel(
            command.EntityId.ContractId,
            command.EntityId.ValueDate,
            checked((current.PublishedSnapshot?.Revision ?? 0) + 1),
            command.SourceEventTimestamp,
            command.FuturesEodData,
            tradeSignal,
            string.Join(", ", missingInputs));
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
        if (missingInputs.Count != 0)
            return null;
        var tradeSignalCommand = new UpdateFuturesTradeSignalCommand(
            futuresEodData,
            state.FuturesRsiSignal!,
            state.FuturesTdiSignal!,
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
        if (state.FuturesRsiSignal is null) missing.Add("RSI");
        if (state.FuturesTdiSignal is null) missing.Add("TDI");
        if (state.TrendDirectionChange is null) missing.Add("ITI direction");
        if (state.TrendExtremeChange is null) missing.Add("ITI extreme");
        if (state.TrendReversalChange is null) missing.Add("ITI reversal");
        if (state.VixFuturesPrice <= 0) missing.Add("VX price");
        return missing;
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
