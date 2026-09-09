using System.Diagnostics;
using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Transport-only W3C context; never serialized into business commands or events.</summary>
public static class ActorTrace
{
    public const string SourceName = "TomasAI.IFM.ActorTracing";
    public static readonly ActivitySource Source = new(SourceName);
    public static Activity? Start(Contracts.IActorMessage message)
    {
        // A worker may inherit its startup context; only this message owns processing causality.
        Activity.Current = null;
        var parent = message.TraceContext;
        // Only bounded workflow commands originate roots. Other actors continue supplied context.
        var span = parent != default || message.Subject.Name == "IntrinsicTimeStrategyWorkflowCommand"
            ? Source.StartActivity("actor.process", ActivityKind.Consumer, parent) : null;
        span?.SetTag("ifm.workflow.entity", message.Subject.EntityId);
        span?.SetTag("ifm.actor.name", message.Subject.Name);
        span?.SetTag("ifm.actor.verb", message.Subject.Verb);
        return span;
    }
    public static NatsHeaders? Headers()
    {
        if (Activity.Current is not { IdFormat: ActivityIdFormat.W3C } activity) return null;
        var headers = new NatsHeaders { ["traceparent"] = activity.Id! };
        if (!string.IsNullOrEmpty(activity.TraceStateString)) headers["tracestate"] = activity.TraceStateString;
        return headers;
    }
    public static ActivityContext Extract(NatsHeaders? headers)
    {
        if (headers is null || !headers.TryGetValue("traceparent", out var parent)) return default;
        headers.TryGetValue("tracestate", out var state);
        return ActivityContext.TryParse(parent.ToString(), state.ToString(), true, out var context) ? context : default;
    }
}
