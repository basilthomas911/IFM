using System.Diagnostics;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Transport-only W3C context; never serialized into business commands or events.</summary>
public static class ActorTrace
{
    public const string SourceName = "TomasAI.IFM.ActorTracing";
    public static readonly ActivitySource Source = new(SourceName);
    public static Activity? Start(Contracts.IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        // A worker may inherit its startup context; only this message owns processing causality.
        Activity.Current = null;
        var parent = message.TraceContext;
        // Only bounded workflow commands originate roots. Other actors continue supplied context.
        var span = parent != default || string.Equals(message.Subject.Name, "IntrinsicTimeStrategyWorkflowCommand"
, StringComparison.Ordinal) ? Source.StartActivity("actor.process", ActivityKind.Consumer, parent) : null;
        span?.SetTag("ifm.workflow.entity", message.Subject.EntityId);
        span?.SetTag("ifm.actor.name", message.Subject.Name);
        span?.SetTag("ifm.actor.verb", message.Subject.Verb);
        return span;
    }
}
