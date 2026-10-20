using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Trade.Order.Query;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

public sealed class BrokerTradePreviewQueryTests
{
    static TradeOrderPreviewRequest Draft(decimal? enteredPrice = null) => new(0,
        [new("SC", -1, 2, 110, true, false, 2, 2.4m, .16, .2, 50),
         new("LC", 1, 2, 120, true, false, .5m, .7m, .1, .1, 50)], 2, enteredPrice, .05m, null);

    [Fact]
    public async Task Ui_service_and_actual_client_route_serialized_drafts_to_backend_handler_then_format_server_totals()
    {
        var producer = Substitute.For<IActorProducer>();
        var seen = new List<GetTradeOrderPreviewQuery>();
        producer.RequestAsync<TradeOrderPreview, GetTradeOrderPreviewQuery>(Arg.Any<ActorSubject>(), Arg.Any<GetTradeOrderPreviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<ServiceResult<TradeOrderPreview>>(DispatchAsync(call)));
        async Task<ServiceResult<TradeOrderPreview>> DispatchAsync(NSubstitute.Core.CallInfo call)
        {
                var query = MessagePackSerializer.Deserialize<GetTradeOrderPreviewQuery>(MessagePackSerializer.Serialize(call.Arg<GetTradeOrderPreviewQuery>()));
                seen.Add(query);
                var context = Substitute.For<ITradeOrderQueryContext>();
                ServiceResult<TradeOrderPreview>? reply = null;
                context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, Arg.Do<ServiceResult<TradeOrderPreview>>(value => reply = value));
                await query.ExecuteAsync(context, call.Arg<CancellationToken>());
                return MessagePackSerializer.Deserialize<ServiceResult<TradeOrderPreview>>(MessagePackSerializer.Serialize(reply!));
        }
        var owner = new BrokerTradePreviewQuery(new TradeQueryService(new OptionTradeQueryApi(producer)));
        var first = await owner.ExecuteAsync(Draft());
        Assert.Equal(160m, first.MaximumProfit); Assert.Equal(840m, first.MaximumLoss);
        var changed = await owner.ExecuteAsync(Draft(-1.55m));
        Assert.Equal(155m, changed.MaximumProfit); Assert.Equal(845m, changed.MaximumLoss);
        Assert.Equal(2, seen.Count); Assert.Equal(1, seen[0].TradeOrderPreviewRequest.InputRevision); Assert.Equal(2, changed.InputRevision);
        Assert.All(seen, query => { Assert.Equal(GetTradeOrderPreviewQuery.Verb, query.Subject.Verb); Assert.Equal(GetTradeOrderPreviewQuery.Actor, query.Subject.Name); });
        var presentation = BrokerTradePreviewPresentation.FromBackend([], changed, null);
        Assert.Equal(-1.55m, presentation.OrderPrice);
        Assert.Equal(845m, decimal.Parse(presentation.Fields["Max loss"]));
    }

    [Fact]
    public async Task Failed_or_mismatched_responses_have_no_local_fallback()
    {
        var api = Substitute.For<ITradeQueryApi>();
        var owner = new BrokerTradePreviewQuery(new TradeQueryService(api));
        api.GetTradeOrderPreviewAsync(Arg.Any<TradeOrderPreviewRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceFailed<TradeOrderPreview>(503, "Backend unavailable"));
        Assert.Contains("503", (await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteAsync(Draft()))).Message);
        api.GetTradeOrderPreviewAsync(Arg.Any<TradeOrderPreviewRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TradeOrderPreview>(new() { InputRevision = 999 }));
        Assert.Contains("revision", (await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteAsync(Draft()))).Message);
    }

    [Fact]
    public async Task Late_old_draft_response_cannot_overwrite_newer_totals()
    {
        var api = Substitute.For<ITradeQueryApi>();
        var owner = new BrokerTradePreviewQuery(new TradeQueryService(api));
        var old = new TaskCompletionSource<ServiceResult<TradeOrderPreview>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetTradeOrderPreviewAsync(Arg.Any<TradeOrderPreviewRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<TradeOrderPreviewRequest>().InputRevision == 1 ? old.Task : Task.FromResult<ServiceResult<TradeOrderPreview>>(new ServiceOk<TradeOrderPreview>(new() { InputRevision = 2, OrderPrice = -1.55m })));
        var pending = owner.ExecuteAsync(Draft());
        var current = await owner.ExecuteAsync(Draft(-1.55m)); Assert.Equal(-1.55m, current.OrderPrice);
        old.SetResult(new ServiceOk<TradeOrderPreview>(new() { InputRevision = 1, OrderPrice = -1.6m }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
    }

    [Fact]
    public async Task Cancelled_view_does_not_apply_a_late_successful_response()
    {
        var api = Substitute.For<ITradeQueryApi>();
        using var cancellation = new CancellationTokenSource();
        api.GetTradeOrderPreviewAsync(Arg.Any<TradeOrderPreviewRequest>(), cancellation.Token)
            .Returns(call => { cancellation.Cancel(); return Task.FromResult<ServiceResult<TradeOrderPreview>>(new ServiceOk<TradeOrderPreview>(new() { InputRevision = 1 })); });
        var owner = new BrokerTradePreviewQuery(new TradeQueryService(api));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.ExecuteAsync(Draft(), cancellation.Token));
    }
}

