using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Defines the JetStream operations required by <see cref="NatsJSDurableReplayQueue"/>.
/// </summary>
internal interface INatsJSDurableQueueTransport : IAsyncDisposable
{
    /// <summary>Creates or updates the streams and durable consumers for a projector queue.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="settings">The resource names and replay policy to apply.</param>
    /// <param name="cancellationToken">A token that cancels initialization.</param>
    /// <returns>A value task that completes when the resources and local consumer handles are ready.</returns>
    ValueTask EnsureQueueAsync(string eventProjectorName, NatsJSDurableQueueSettings settings, CancellationToken cancellationToken);

    /// <summary>Publishes a serialized event to an initialized projector's process subject.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="payload">The serialized durable event envelope.</param>
    /// <param name="messageId">The stable JetStream message identifier used for duplicate suppression.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>A value task that completes after the server acknowledges the publication.</returns>
    ValueTask PublishProcessAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken);

    /// <summary>Publishes a serialized failed event to an initialized projector's replay subject.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="payload">The serialized durable event envelope, including failure details.</param>
    /// <param name="messageId">The stable JetStream message identifier used for duplicate suppression.</param>
    /// <param name="cancellationToken">A token that cancels publication.</param>
    /// <returns>A value task that completes after the server acknowledges the publication.</returns>
    ValueTask PublishReplayAsync(
        string eventProjectorName,
        byte[] payload,
        string messageId,
        CancellationToken cancellationToken);

    /// <summary>Consumes messages from an initialized projector's process consumer.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="cancellationToken">A token that stops the asynchronous enumeration.</param>
    /// <returns>An asynchronous sequence of process messages.</returns>
    IAsyncEnumerable<INatsJSDurableMessage> ConsumeProcessAsync(string eventProjectorName, CancellationToken cancellationToken);

    /// <summary>Consumes messages from an initialized projector's replay consumer.</summary>
    /// <param name="eventProjectorName">The projector key used to locate the initialized queue.</param>
    /// <param name="cancellationToken">A token that stops the asynchronous enumeration.</param>
    /// <returns>An asynchronous sequence of replay messages.</returns>
    IAsyncEnumerable<INatsJSDurableMessage> ConsumeReplayAsync(string eventProjectorName, CancellationToken cancellationToken);
}
