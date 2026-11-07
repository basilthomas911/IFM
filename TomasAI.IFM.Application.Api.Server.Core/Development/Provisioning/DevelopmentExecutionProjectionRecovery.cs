using System.Net;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Recovery;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Server.Core.Development.Provisioning;

/// <summary>Provides explicit local Development recovery of already-filled closing executions.</summary>
internal static class DevelopmentExecutionProjectionRecovery
{
    internal static void Map(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        app.MapPost("/development/portfolios/{portfolio:int}/funds/{fund:int}/orders/{order:int}/projections/{eventId:long}/retry",
            async (HttpContext http, int portfolio, int fund, int order, long eventId, CancellationToken token) =>
            {
                if (http.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                var expected = new TradeOrderId(portfolio, fund, order);
                if (!expected.IsValid || eventId <= 0) return Results.BadRequest("Exact order and source event required.");
                var container = app.Services.GetRequiredService<SimpleInjector.Container>();
                var projector = container.GetInstance<IEventProjector<OrderExecutionCommandActor>>();
                var source = await projector.DbEventSource.GetEventLogByEventIdAsync(eventId, token).ConfigureAwait(false);
                if (source is null || !OrderExecutionProjectionRecovery.CanRetry(source.ToDomainEvent(), expected))
                    return Results.BadRequest("Only a matching committed terminal close with fills can be retried.");
                var accepted = await projector.RetryExactAsync(eventId, token).ConfigureAwait(false);
                app.Logger.LogInformation("Development close settlement recovery Order={Order}; SourceEvent={Event}; Accepted={Accepted}", expected.Format(), eventId, accepted);
                return accepted ? Results.Accepted(value: new { eventId, order, accepted })
                    : Results.Conflict("Projection is already complete or currently owned by another attempt.");
            });
    }
}
