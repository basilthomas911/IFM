using System.Diagnostics;
using NATS.Client.Core;
namespace TomasAI.IFM.Framework.Messaging.Nats;
public static class NatsActorTrace
{
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
