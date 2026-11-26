using TomasAI.IFM.Shared.Diagnostics;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Logging;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

public static class FuturesTickTradeDataChanged
{

    extension(FuturesTickTradeDataChangedEvent changed)
    {
        public async ValueTask ExecuteAsync(IFuturesOptionRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(changed);
            ArgumentNullException.ThrowIfNull(context);
            await context.RoutingReceipts.SerializeAsync(FuturesOptionRoutingReceipts.Contract(changed), async () =>
            {
                await RecoverAsync(context, FuturesOptionRoutingReceipts.Contract(changed)).ConfigureAwait(false);
                var receipt=context.RoutingReceipts.Find(changed);
                if(receipt?.Completed==true)return;
                if(receipt is null)
                {
                    if(changed.OptionMarketPriceObservation?.OptionTickData is {} data && !StrategyPositionLegOptionData.IsValidQuote(data,data.ContractId))return;
                    var receivedAtUtc=changed.ReceivedOn.Kind==DateTimeKind.Utc?changed.ReceivedOn:DateTime.SpecifyKind(changed.ReceivedOn,DateTimeKind.Utc);
                    var observation=changed.OptionMarketPriceObservation;
                    var tick=new PositionMarketTick(FuturesOptionRoutingReceipts.Contract(changed),observation is null?changed.TradeData.Price:(decimal)observation.OptionTickData.OptionPrice,FuturesOptionRoutingReceipts.Sequence(changed),receivedAtUtc);
                    // Reset/repeated provider sequences remain distinct arrivals; only exact observation identities deduplicate.
                    var outcome=context.RouteIndex.TryRoute(in tick,out var routes,enforceSequenceOrdering:false);
                    if(outcome is not(MarketRouteLookupOutcome.Routed or MarketRouteLookupOutcome.NoOpenPosition or MarketRouteLookupOutcome.UnknownInstrument))
                        throw new InvalidDataException($"Invalid option routing input: {outcome}.");
                    var commands=(routes??[]).Where(FuturesOptionRoutePolicy.IsSupported).Select(route=>CreateCommand(changed,tick,route)).ToArray();
                    receipt=context.RoutingReceipts.Prepare(changed,commands);
                    if(outcome!=MarketRouteLookupOutcome.Routed)LogIgnoredTick(context,tick.ContractId,outcome);
                }
                await DispatchAsync(context,receipt).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }

    public static async ValueTask RecoverAsync(IFuturesOptionRealtimeContext context,string? contract=null)
    {
        foreach(var receipt in context.RoutingReceipts.Pending(contract))await DispatchAsync(context,receipt).ConfigureAwait(false);
    }
    static async ValueTask DispatchAsync(IFuturesOptionRealtimeContext context,FuturesOptionRoutingReceipt receipt)
    {
        var contract=FuturesOptionRoutingReceipts.Contract(receipt.Source);
        try
        {
            await DispatchRequiredAsync(context,receipt).ConfigureAwait(false);
            context.RoutingProgress?.Record(contract,true,DateTime.UtcNow);
        }
        catch {context.RoutingProgress?.Record(contract,false,DateTime.UtcNow);throw;}
    }
    static async ValueTask DispatchRequiredAsync(IFuturesOptionRealtimeContext context,FuturesOptionRoutingReceipt receipt)
    {
        var id=FuturesOptionRoutingReceipts.Identity(receipt.Source);
        // The durable source/commands precede cache mutation. No notification is sent until its exact payload is durable.
        if(!receipt.PublicationsCaptured)
        {
            var publications=(receipt.Source.OptionMarketPriceObservation?.OptionTickData is {} data
                ? context.RealtimeCache?.Apply(data)??[]:[]).Select(snapshot=>
                {
                    var publication=TradeRealtimeSnapshotChangedEvent.From(snapshot);
                    MessageOrigins.Capture(publication,TimeProvider.System,causationId:receipt.Source.Id);
                    return publication;
                }).ToArray();
            context.RoutingReceipts.CapturePublications(id,publications);
            receipt=context.RoutingReceipts.Find(receipt.Source)!;
        }
        for(var branch=0;branch<receipt.Publications.Length;branch++)
        {
            if(receipt.Published[branch])continue;
            await context.SendAsync<TradeRealtimeSnapshotChangedEvent,TradeEntityId>(receipt.Publications[branch]).ConfigureAwait(false);
            context.RoutingReceipts.ConfirmPublication(id,branch);
        }
        for(var branch=0;branch<receipt.Commands.Length;branch++)
        {
            if(receipt.Stages[branch]!=OptionRouteStage.Pending)continue;
            var command=receipt.Commands[branch];
            if(!context.RouteIndex.IsCurrentRoute(command))
            {
                if(!context.RouteIndex.IsSuperseded(command))throw new InvalidOperationException("Option route evidence is missing, behind, or conflicts with the captured target.");
                context.RoutingReceipts.SetStage(id,branch,OptionRouteStage.Superseded);continue;
            }
            using var step=new OptionFeedTrace("option.quote.await_position_command",context.Logger,$"{command.CommandId}|{command.EntityId.Format()}|{command.ContractId}",realtime:true);
            var result=await context.ActorService.SendAsync<ChangeTradeLegDataCommand,StrategyPositionId>(command,command.EntityId).ConfigureAwait(false);
            if(!result.Success||result.Value!=command.CommandId)throw new InvalidOperationException($"Option position dispatch unconfirmed for {command.ContractId}, position {command.EntityId.Format()}: {result.ErrorCode}: {result.ErrorMessage}");
            context.RoutingReceipts.SetStage(id,branch,OptionRouteStage.Confirmed);
            step.Complete();
        }
        context.RoutingReceipts.Complete(id);
    }
    static ChangeTradeLegDataCommand CreateCommand(FuturesTickTradeDataChangedEvent changed,PositionMarketTick tick,PortfolioFundTradeLeg route)
    {
        var positionId=new StrategyPositionId(new TradeEntityId(route.PortfolioId,route.FundId,route.OrderId,route.TradeId),route.StrategyPositionId);
        var actorName=route.TradeType==TradeStrategyKind.IronCondor?FuturesIronCondorTradePositionCommandActor.ActorName:FuturesVerticalSpreadTradePositionCommandActor.ActorName;
        var command=new ChangeTradeLegDataCommand
        {
            CommandId=TickCommandId(FuturesOptionRoutingReceipts.Identity(changed),route.StrategyPositionId,route.TradeLegId),
            Subject=new ActorSubject(ActorType.Command,actorName,ChangeTradeLegDataCommand.Verb,positionId.Format()),
            EntityId=positionId,TradeLegId=route.TradeLegId,ContractId=tick.ContractId,Price=tick.Price,
            SourceSequence=tick.SourceSequence,RouteGeneration=route.Generation,EffectiveAtUtc=tick.OccurredAtUtc,
            OptionTick=changed.OptionMarketPriceObservation?.OptionTickData,TradeType=route.TradeType
        };
        MessageOrigins.Capture(command,TimeProvider.System,causationId:changed.Id);return command;
    }

    static void LogIgnoredTick(IFuturesOptionRealtimeContext context, string contractId, MarketRouteLookupOutcome outcome)
    {
        if (!context.RouteIndex.ShouldLog(contractId, outcome))
            return;
        if (outcome == MarketRouteLookupOutcome.UnknownInstrument)
            FuturesTickTradeDataChangedLogging.UnknownContract(context.Logger, contractId);
        else if (outcome == MarketRouteLookupOutcome.NoOpenPosition)
            FuturesTickTradeDataChangedLogging.NoOpenPosition(context.Logger, contractId);
    }

    static Guid TickCommandId(Guid sourceEventId, Guid positionId, Guid legId)
    {
        Span<byte> result = stackalloc byte[16];
        Span<byte> value = stackalloc byte[16];
        sourceEventId.TryWriteBytes(result);
        positionId.TryWriteBytes(value);
        for (var index = 0; index < 16; index++)
            result[index] ^= value[index];
        legId.TryWriteBytes(value);
        for (var index = 0; index < 16; index++)
            result[index] ^= value[index];
        return new Guid(result);
    }
}
