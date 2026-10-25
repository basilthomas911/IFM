using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Application.EventServices;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>Single-desktop bounded queue delivery and displayed-batch acknowledgements.</summary>
public static class StatusConsoleEndpoints
{
    public static void MapStatusConsole(this WebApplication app)
    {
        // One setup call after the UI subscribes and on reconnect. Messages are pushed by NATS.
        app.MapPost("/api/status-console/connect", async (NatsConnectionManager connection, INatsProducerOptions options,
            CancellationToken cancellationToken) =>
        {
            var js = await connection.GetJetStreamContextAsync(options.Url, cancellationToken).ConfigureAwait(false);
            await StatusConsoleHistory.EnsureAsync(js, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { DeliverySubject = StatusConsoleHistory.DeliverySubject });
        });
        app.MapPost("/api/status-console/ack", async (NatsConnectionManager connection, INatsProducerOptions options,
            StatusConsoleHistory.DisplayedBatch batch, CancellationToken cancellationToken) =>
        {
            try
            {
                await StatusConsoleHistory.AcknowledgeAsync(connection, options, batch, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { Acknowledged = batch.Acknowledgements.Length });
            }
            catch (ArgumentException error) { return Results.BadRequest(new { Error = error.Message }); }
        });
    }
}
