using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command;

/// <summary>Commits the failed fact for an accepted historical load attempt.</summary>
public static class FailFuturesAnalyticsHistoricalDataLoader
{
    extension(FailFuturesAnalyticsHistoricalDataLoaderCommand command)
    {
        /// <summary>Applies the terminal fact once and preserves a completed attempt on redelivery.</summary>
        public ServiceResult<GuidResult> Execute(FuturesAnalyticsHistoricalDataLoaderCommandState state)
        {
            if (!state.IsRequested)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "Historical data load was never requested.");
            if (state.IsCompleted || state.IsFailed)
                return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
            if (string.IsNullOrWhiteSpace(command.ErrorMessage))
                return new ServiceFailed<GuidResult>(command.ErrorCode, "Historical load failure evidence is missing.");

            var fact = new FuturesAnalyticsHistoricalDataLoaderFailedEvent
            {
                Subject = new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderFailedEvent.Actor,
                    FuturesAnalyticsHistoricalDataLoaderFailedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                CommandId = command.CommandId,
                ReceivedOn = DateTime.UtcNow,
                ErrorMessage = command.ErrorMessage,
                LastCompletedBatchOrdinal = command.LastCompletedBatchOrdinal,
                LastCompletedRecordOrdinal = command.LastCompletedRecordOrdinal,
            };
            return state.Update(fact, command)
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "Unable to apply historical load outcome.");
        }
    }
}
