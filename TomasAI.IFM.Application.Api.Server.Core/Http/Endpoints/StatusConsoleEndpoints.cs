using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventProducers;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>Uncached cursor reads retain startup messages emitted before a UI connects.</summary>
public static class StatusConsoleEndpoints
{
    public static void MapStatusConsole(this WebApplication app)
    {
        app.MapGet("/api/status-console", async (NatsConnectionManager connection, INatsProducerOptions options,
            ulong? after, ulong? through, int? limit, CancellationToken cancellationToken) =>
        {
            if (limit is < 1 or > 200 || (through.HasValue && after.GetValueOrDefault() > through.Value))
                return Results.BadRequest(new { Error = "Invalid console history page." });
            try
            {
                return Results.Ok(await StatusConsoleHistory.ReadAsync(
                    connection, options, after ?? 0, through, limit ?? 100, cancellationToken).ConfigureAwait(false));
            }
            catch (ArgumentException error) { return Results.BadRequest(new { Error = error.Message }); }
        });
    }
}
