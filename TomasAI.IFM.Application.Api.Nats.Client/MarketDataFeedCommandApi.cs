using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// create market data feed command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class MarketDataFeedCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IMarketDataFeedCommandApi
{
    /// <summary>
    /// Starts a market data feed for the specified futures contracts and value date.
    /// </summary>
    /// <param name="futuresContracts">The collection of futures contracts to include in the feed.</param>
    /// <param name="valueDate">The value date for the market data feed.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required arguments are null.</exception>
    public async Task<ServiceResult<Guid>> StartMarketDataFeedAsync(
        ICollection<FuturesContractV2ReadModel> futuresContracts,
        DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new MarketDataFeedId(valueDate);
            var cmd = new StartMarketDataFeedCommand([.. IsArgumentNull.Set(futuresContracts)], valueDate, false)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartMarketDataFeedCommand.Actor, StartMarketDataFeedCommand.Verb, entityId.Format()),
                ErrorCode = StartMarketDataFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartMarketDataFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Stops the market data feed for the specified value date.
    /// </summary>
    /// <param name="valueDate">The value date of the market data feed to stop.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StopMarketDataFeedAsync(DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new MarketDataFeedId(valueDate);
            var cmd = new StopMarketDataFeedCommand(valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StopMarketDataFeedCommand.Actor, StopMarketDataFeedCommand.Verb, entityId.Format()),
                ErrorCode = StopMarketDataFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopMarketDataFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Resets the market data feed for the specified futures contracts and value date.
    /// </summary>
    /// <param name="futuresContracts">The collection of futures contracts to reset for.</param>
    /// <param name="valueDate">The value date for the reset operation.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> ResetMarketDataFeedAsync(
        ICollection<FuturesContractV2ReadModel> futuresContracts,
        DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new MarketDataFeedId(valueDate);
            var cmd = new ResetMarketDataFeedCommand([.. IsArgumentNull.Set(futuresContracts)], valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ResetMarketDataFeedCommand.Actor, ResetMarketDataFeedCommand.Verb, entityId.Format()),
                ErrorCode = ResetMarketDataFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ResetMarketDataFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts a futures tick data record for a given contract.
    /// </summary>
    /// <param name="futuresContract">The futures contract related to the tick data.</param>
    /// <param name="futuresTickData">The tick data to insert.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertFuturesTickDataAsync(
        FuturesContractV2ReadModel futuresContract,
        FuturesTickDataV2ReadModel futuresTickData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = IsArgumentNull.Set(futuresTickData.DataId);
            var cmd = new InsertFuturesTickDataCommand(IsArgumentNull.Set(futuresContract), IsArgumentNull.Set(futuresTickData))
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertFuturesTickDataCommand.Actor, InsertFuturesTickDataCommand.Verb, entityId.Format()),
                ErrorCode = InsertFuturesTickDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertFuturesTickDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts futures option tick data for a given option contract.
    /// </summary>
    /// <param name="futuresContract">The base futures contract for the option.</param>
    /// <param name="futuresOptionTickData">The option tick data to insert.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertFuturesOptionTickDataAsync(
        FuturesContractV2ReadModel futuresContract,
        FuturesOptionTickDataV2ReadModel futuresOptionTickData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = IsArgumentNull.Set(futuresOptionTickData.EntityId);
            var cmd = new InsertFuturesOptionTickDataCommand(IsArgumentNull.Set(futuresContract), IsArgumentNull.Set(futuresOptionTickData))
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
                ErrorCode = InsertFuturesOptionTickDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertFuturesOptionTickDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Starts streaming of futures option tick data for the specified entity and contracts.
    /// </summary>
    /// <param name="entityId">The entity id for the futures option tick data stream.</param>
    /// <param name="optionContract">The option contract information.</param>
    /// <param name="baseContract">The base futures contract information.</param>
    /// <param name="valueDate">The value date for the streaming request.</param>
    /// <param name="maturityDate">The maturity date for the option.</param>
    /// <param name="riskFreeRate">The risk-free rate used in streaming parameters.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StartFuturesOptionTickDataStreamingAsync(
            FuturesOptionTickEntityId entityId,
            FuturesOptionContractReadModel optionContract,
            FuturesContractV2ReadModel baseContract,
            DateOnly valueDate,
            DateOnly maturityDate,
            double riskFreeRate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new StartFuturesOptionTickDataStreamingCommand(entityId, IsArgumentNull.Set(optionContract), IsArgumentNull.Set(baseContract), valueDate, maturityDate, riskFreeRate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StartFuturesOptionTickDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartFuturesOptionTickDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Stops streaming of futures option tick data for the specified entity.
    /// </summary>
    /// <param name="entityId">The entity id of the streaming request to stop.</param>
    /// <param name="contractId">The contract id associated with the stream.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StopFuturesOptionTickDataStreamingAsync(FuturesOptionTickEntityId entityId, string contractId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new StopFuturesOptionTickDataStreamingCommand(entityId, IsArgumentNull.Set(contractId))
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StopFuturesOptionTickDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopFuturesOptionTickDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Deletes a streaming request identifier for the given feed.
    /// </summary>
    /// <param name="feedId">The feed id whose streaming request id will be deleted.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> DeleteStreamingRequestIdAsync(FeedId feedId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FeedId(feedId.Value);
            var cmd = new DeleteStreamingRequestIdCommand(feedId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, DeleteStreamingRequestIdCommand.Actor, DeleteStreamingRequestIdCommand.Verb, entityId.Format()),
                ErrorCode = DeleteStreamingRequestIdCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, DeleteStreamingRequestIdCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Starts streaming of futures tick data for a given contract and value date.
    /// </summary>
    /// <param name="futuresContract">The futures contract to stream.</param>
    /// <param name="valueDate">The value date for streaming.</param>
    /// <param name="resetStream">Whether to reset the stream before starting.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StartFuturesTickDataStreamingAsync(
        FuturesContractV2ReadModel futuresContract,
        DateOnly valueDate,
        bool resetStream)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesDataId(futuresContract?.ContractId ?? string.Empty, valueDate);
            var cmd = new StartFuturesTickDataStreamingCommand(IsArgumentNull.Set(futuresContract), valueDate, resetStream)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartFuturesTickDataStreamingCommand.Actor, StartFuturesTickDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StartFuturesTickDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartFuturesTickDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Stops streaming of futures tick data for the specified contract and date.
    /// </summary>
    /// <param name="contractId">The contract id whose streaming will be stopped.</param>
    /// <param name="valueDate">The value date associated with the streaming to stop.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StopFuturesTickDataStreamingAsync(string contractId, DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesDataId(contractId, valueDate);
            var cmd = new StopFuturesTickDataStreamingCommand(IsArgumentNull.Set(contractId), valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StopFuturesTickDataStreamingCommand.Actor, StopFuturesTickDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StopFuturesTickDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopFuturesTickDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Starts streaming of futures bar data for the specified contracts and value date.
    /// </summary>
    /// <param name="futuresContracts">Array of futures contracts to stream bar data for.</param>
    /// <param name="valueDate">The value date for the bar data stream.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StartFuturesBarDataStreamingAsync(
        FuturesContractV2ReadModel[] futuresContracts,
        DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesBarDataStreamingId(valueDate);
            var cmd = new StartFuturesBarDataStreamingCommand(IsArgumentNull.Set(futuresContracts), valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StartFuturesBarDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartFuturesBarDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Stops streaming of futures bar data for the specified value date.
    /// </summary>
    /// <param name="valueDate">The value date of the bar data stream to stop.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StopFuturesBarDataStreamingAsync(DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesBarDataStreamingId(valueDate);
            var cmd = new StopFuturesBarDataStreamingCommand(valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StopFuturesBarDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopFuturesBarDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Enables the live trade feed for the specified order and trade.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> EnableTradeLiveFeedAsync(int orderId, int tradeId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var valueDateLocal = DateOnly.FromDateTime(DateTime.UtcNow);
            var entityId = new TradeLiveFeedId(orderId, tradeId, valueDateLocal);
            var cmd = new TurnTradeLiveFeedOnCommand(orderId, tradeId, valueDateLocal)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, TurnTradeLiveFeedOnCommand.Actor, TurnTradeLiveFeedOnCommand.Verb, entityId.Format()),
                ErrorCode = TurnTradeLiveFeedOnCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, TurnTradeLiveFeedOnCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Disables the live trade feed for the specified order and trade.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> DisableTradeLiveFeedAsync(int orderId, int tradeId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var valueDateLocalOff = DateOnly.FromDateTime(DateTime.UtcNow);
            var entityId = new TradeLiveFeedId(orderId, tradeId, valueDateLocalOff);
            var cmd = new TurnTradeLiveFeedOffCommand(orderId, tradeId, valueDateLocalOff)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, TurnTradeLiveFeedOffCommand.Actor, TurnTradeLiveFeedOffCommand.Verb, entityId.Format()),
                ErrorCode = TurnTradeLiveFeedOffCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, TurnTradeLiveFeedOffCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Adds a trade to the live feed for the specified order and trade id on the given date.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The date for the live feed addition.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> AddTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityIdAdd = new TradeOrderId(orderId, tradeId);
            var cmd = new AddTradeLiveFeedCommand(orderId, tradeId, valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddTradeLiveFeedCommand.Actor, AddTradeLiveFeedCommand.Verb, entityIdAdd.Format()),
                ErrorCode = AddTradeLiveFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityIdAdd);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddTradeLiveFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Removes a trade from the live feed for the specified order and trade id on the given date.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The date for the live feed removal.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedAsync(int orderId, int tradeId, DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new TradeOrderId(orderId, tradeId);
            var cmd = new RemoveTradeLiveFeedCommand(orderId, tradeId, valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveTradeLiveFeedCommand.Actor, RemoveTradeLiveFeedCommand.Verb, entityId.Format()),
                ErrorCode = RemoveTradeLiveFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveTradeLiveFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Removes a trade from the live feed for the specified order and trade id on the given date.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> RemoveTradeLiveFeedsAsync(int orderId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new TradeLiveFeedsId(orderId);
            var cmd = new RemoveTradeLiveFeedsCommand(orderId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveTradeLiveFeedCommand.Actor, RemoveTradeLiveFeedCommand.Verb, entityId.Format()),
                ErrorCode = RemoveTradeLiveFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveTradeLiveFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Halts the live trade feed for the specified order and trade.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> HaltTradeLiveFeedAsync(int orderId, int tradeId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityIdHalt = new TradeOrderId(orderId, tradeId);
            var cmd = new HaltTradeLiveFeedCommand(orderId, tradeId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, HaltTradeLiveFeedCommand.Actor, HaltTradeLiveFeedCommand.Verb, entityIdHalt.Format()),
                ErrorCode = HaltTradeLiveFeedCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityIdHalt);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, HaltTradeLiveFeedCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts end-of-day (EOD) futures data and related series for a given value date.
    /// </summary>
    /// <param name="valueDate">The value date for the EOD data.</param>
    /// <param name="futuresTickData">Tick data used to build EOD records.</param>
    /// <param name="contract">The futures contract information.</param>
    /// <param name="eodDataToday">EOD data for the given date.</param>
    /// <param name="eodDataRange">A collection of historical EOD data used for calculations.</param>
    /// <param name="normCurveData">Normal curve data used in processing.</param>
    /// <param name="windowSize">Window size for any rolling calculations.</param>
    /// <param name="vixEodData">VIX futures EOD data related to the instrument.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertFuturesEodDataAsync(
        DateOnly valueDate,
        FuturesTickDataV2ReadModel futuresTickData,
        FuturesContractV2ReadModel contract,
        FuturesEodDataV2ReadModel eodDataToday,
        ICollection<FuturesEodDataV2ReadModel> eodDataRange,
        NormalCurveTableReadModel normCurveData,
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesEodDataId(futuresTickData.ContractId, valueDate);
            var cmd = new InsertFuturesEodDataCommand(valueDate, futuresTickData, contract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
                ErrorCode = InsertFuturesEodDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertFuturesEodDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts VIX futures end-of-day data.
    /// </summary>
    /// <param name="vixFuturesTickData">The VIX futures tick data to insert as EOD data.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel vixFuturesTickData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesEodDataId(vixFuturesTickData.ContractId, vixFuturesTickData.ValueDate);
            var cmd = new InsertVixFuturesEodDataCommand(IsArgumentNull.Set(vixFuturesTickData))
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
                ErrorCode = InsertVixFuturesEodDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertVixFuturesEodDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts futures bar data record.
    /// </summary>
    /// <param name="futuresBarData">The bar data to insert.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesBarDataId(futuresBarData.ContractId, futuresBarData.Symbol, futuresBarData.ValueDate);
            var cmd = new InsertFuturesBarDataCommand(IsArgumentNull.Set(futuresBarData))
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
                ErrorCode = InsertFuturesBarDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertFuturesBarDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts the closing price for a futures instrument.
    /// </summary>
    /// <param name="id">The identifier for the futures data (contract and date).</param>
    /// <param name="closingPrice">The closing price to insert.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertFuturesClosingPriceAsync(
        FuturesDataId id,
        decimal closingPrice)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new InsertFuturesClosingPriceCommand(IsArgumentNull.Set(id), closingPrice)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, id.Format()),
                ErrorCode = InsertFuturesClosingPriceCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, id);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertFuturesClosingPriceCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Deletes futures bar data identified by the provided id.
    /// </summary>
    /// <param name="id">The bar data identifier to delete.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> DeleteFuturesBarDataAsync(FuturesBarDataId id)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new DeleteFuturesBarDataCommand(id)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, id.Format()),
                ErrorCode = DeleteFuturesBarDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, id);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, DeleteFuturesBarDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Inserts quote data for a futures option.
    /// </summary>
    /// <param name="quoteId">The quote identifier.</param>
    /// <param name="contractId">The contract identifier for the option.</param>
    /// <param name="quoteData">The quote payload to insert.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> InsertFuturesOptionQuoteDataAsync(int quoteId, string contractId, QuoteData quoteData)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new QuoteId(quoteId);
            var cmd = new InsertFuturesOptionQuoteDataCommand(quoteId, contractId, quoteData)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionQuoteDataCommand.Actor, InsertFuturesOptionQuoteDataCommand.Verb, entityId.Format()),
                ErrorCode = InsertFuturesOptionQuoteDataCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertFuturesOptionQuoteDataCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Starts streaming of futures option quote data for a given quote id.
    /// </summary>
    /// <param name="quoteId">The quote identifier for the streaming request.</param>
    /// <param name="futuresOptionQuotes">The array of option quotes to stream.</param>
    /// <param name="futuresOptionContracts">The option contracts related to the quotes.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StartFuturesOptionQuoteDataStreamingAsync(
        int quoteId, FuturesOptionQuoteReadModel[] futuresOptionQuotes, FuturesOptionContractReadModel[] futuresOptionContracts)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new QuoteId(quoteId);
            var cmd = new StartFuturesOptionQuoteDataStreamingCommand(quoteId, futuresOptionQuotes, futuresOptionContracts)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartFuturesOptionQuoteDataStreamingCommand.Actor, StartFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StartFuturesOptionQuoteDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartFuturesOptionQuoteDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Stops streaming of futures option quote data for the specified quote id.
    /// </summary>
    /// <param name="quoteId">The quote identifier of the streaming request to stop.</param>
    /// <returns>A <see cref="ServiceResult{Guid}"/> containing the command id if successful.</returns>
    public async Task<ServiceResult<Guid>> StopFuturesOptionQuoteDataStreamingAsync(int quoteId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new QuoteId(quoteId);
            var cmd = new StopFuturesOptionQuoteDataStreamingCommand(quoteId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StopFuturesOptionQuoteDataStreamingCommand.Actor, StopFuturesOptionQuoteDataStreamingCommand.Verb, entityId.Format()),
                ErrorCode = StopFuturesOptionQuoteDataStreamingCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StopFuturesOptionQuoteDataStreamingCommand.ErrorId);
        }
        return serviceResult;
    }
}
