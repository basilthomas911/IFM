using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

internal sealed record JetStreamMaintenanceConsumer(
    string Name,
    ulong Pending,
    ulong AcknowledgementPending);
