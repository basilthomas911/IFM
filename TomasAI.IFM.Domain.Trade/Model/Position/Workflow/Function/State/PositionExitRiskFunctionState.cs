using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;

/// <summary>Rehydrates and tracks the last committed result for one position exit-risk stream.</summary>
public sealed class PositionExitRiskFunctionState : BaseEventSourceActorState<PositionExitRiskFunctionState>,
    IEventSourceFunctionState<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand,
        PositionExitRiskCompletedEvent>
{
    Guid activeCommandId;
    PositionExitRiskCompletedEvent? latest;
    public override ActorThreadId Id { get; set; } = default!;
    public PositionExitRiskCompletedEvent? CompletedEvent => latest?.CommandId == activeCommandId ? latest : null;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }
    /// <summary>Selects the active command whose completion may be returned.</summary>
    /// <param name="request">The current position exit-risk request.</param>
    /// <returns>This prepared state instance.</returns>
    public PositionExitRiskFunctionState Prepare(EvaluatePositionExitRiskCommand request)
    {
        activeCommandId = request.CommandId;
        return this;
    }
    /// <summary>Determines whether the committed completion matches the current request fingerprint.</summary>
    /// <param name="request">The request to compare.</param>
    /// <returns><see langword="true"/> when the request is an exact replay.</returns>
    public bool Matches(EvaluatePositionExitRiskCommand request) =>
        CompletedEvent?.RequestFingerprint == request.InputHash;
    /// <summary>Applies a new completion when the active request has not already completed.</summary>
    /// <param name="completed">The completion event to apply.</param>
    /// <param name="request">The command that produced the event.</param>
    /// <returns><see langword="true"/> when the state accepted the event.</returns>
    public bool TryComplete(PositionExitRiskCompletedEvent completed,
        EvaluatePositionExitRiskCommand request) => !IsCompleted && Update(completed, request);
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not PositionExitRiskCompletedEvent completed) return false;
        latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
