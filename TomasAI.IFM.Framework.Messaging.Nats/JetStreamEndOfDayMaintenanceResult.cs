using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Describes all transport messages removed by one end-of-day run.</summary>
public sealed record JetStreamEndOfDayMaintenanceResult(
    IReadOnlyList<JetStreamEndOfDayStreamResult> Streams)
{
    /// <summary>Gets the total number of purged messages.</summary>
    public ulong PurgedMessages => Streams.Aggregate(0UL, static (total, stream) => checked(total + stream.PurgedMessages));
}
