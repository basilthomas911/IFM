using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command;

/// <summary>Validates and applies one recovery audit append.</summary>
public static class AppendMarketDataFeedRecovery
{
    extension(AppendMarketDataFeedRecoveryCommand request)
    {
        /// <summary>Validates the delivery envelope against its recovery fact.</summary>
        internal List<ValidationError> Validate()
        {
            var fact = RecoveryRecord.Parse(request.Payload, request.Digest);
            if (request.EntityId.Value != fact.OperationId || request.AttemptId == Guid.Empty
                || request.Subject != new ActorSubject(ActorType.Command, AppendMarketDataFeedRecoveryCommand.Actor,
                    AppendMarketDataFeedRecoveryCommand.Verb, request.EntityId.Format())
                || request.AttemptStartedAtUtc.Kind != DateTimeKind.Utc || request.RequestDeadlineUtc.Kind != DateTimeKind.Utc
                || request.RequestDeadlineUtc <= request.AttemptStartedAtUtc)
                throw new InvalidDataException("Invalid recovery append route/attempt/deadline.");
            return [];
        }

        /// <summary>Applies the audit fact before its delivery deadline expires.</summary>
        internal ServiceResult<GuidResult> Execute(MarketDataFeedRecoveryState state)
        {
            if (request.RequestDeadlineUtc <= DateTime.UtcNow)
                throw new TimeoutException("Recovery audit attempt expired before append.");
            state.Accept(request);
            return new(new GuidResult(request.CommandId));
        }
    }
}
