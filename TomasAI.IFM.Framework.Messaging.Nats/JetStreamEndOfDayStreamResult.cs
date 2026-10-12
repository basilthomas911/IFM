using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Describes one completed end-of-day stream purge.</summary>
public sealed record JetStreamEndOfDayStreamResult(
    string StreamName,
    ulong CapturedLastSequence,
    ulong CapturedMessages,
    ulong PurgedMessages);
