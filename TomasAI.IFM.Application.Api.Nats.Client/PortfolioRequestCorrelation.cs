using System.Diagnostics;

namespace TomasAI.IFM.Application.Api.Nats.Client;

static class PortfolioRequestCorrelation
{
    public static Guid CurrentOrNew()
    {
        var activity = Activity.Current;
        if (activity is null || activity.TraceId == default)
            return Guid.NewGuid();
        return Guid.ParseExact(activity.TraceId.ToHexString(), "N");
    }
}
