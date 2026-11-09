using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command;

/// <summary>Commits the completed fact for an accepted historical load attempt.</summary>
public static class CompleteFuturesAnalyticsHistoricalDataLoader
{
    extension(CompleteFuturesAnalyticsHistoricalDataLoaderCommand command)
    {
        /// <summary>Applies the terminal fact once and preserves a completed attempt on redelivery.</summary>
        public ServiceResult<GuidResult> Execute(FuturesAnalyticsHistoricalDataLoaderCommandState state)
        {
            if (!state.IsRequested)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "Historical data load was never requested.");
            if (state.IsCompleted)
                return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
            if (string.IsNullOrWhiteSpace(command.RequestSha256) || command.CompletedAtUtc == default
                || command.CompletedAtUtc.Kind != DateTimeKind.Utc || command.ValidSessionCount < 0
                || command.GapCount < 0 || command.RollCount < 0)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "Historical load completion evidence is invalid.");

            var fact = new FuturesAnalyticsHistoricalDataLoaderCompletedEvent
            {
                Subject = new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Actor,
                    FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                CommandId = command.CommandId,

                ManifestId = command.ManifestId,
                ValidSessionCount = command.ValidSessionCount,
                GapCount = command.GapCount,
                RollCount = command.RollCount,
                RequestSha256 = command.RequestSha256,
                CompletedAtUtc = command.CompletedAtUtc,
                LastCompletedBatchOrdinal = command.LastCompletedBatchOrdinal,
                LastCompletedRecordOrdinal = command.LastCompletedRecordOrdinal,
            };
            return state.Update(fact, command)
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "Unable to apply historical load outcome.");
        }
    }
}
