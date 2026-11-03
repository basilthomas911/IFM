using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.QueryParameters;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

public static class IEventActorContextExtensions
{

    extension(IEventActorContext context)
    {
        internal async ValueTask<YieldCurveRateReadModel> GetLastYieldCurveRateAsync()
        {
            var entityId = new GetLastYieldCurveRateParameter();
            GetLastYieldCurveRateQuery query = new(true)
            {
                Subject = new ActorSubject(ActorType.Query, GetLastYieldCurveRateQuery.Actor, GetLastYieldCurveRateQuery.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = GetLastYieldCurveRateQuery.ErrorId,
                QueryParams = entityId.Format()
            };
            var serviceResult = await (context.RequestAsync<YieldCurveRateReadModel, GetLastYieldCurveRateQuery>(query)).ConfigureAwait(false);
            return (serviceResult.Success && serviceResult.Value is not null)
                ? serviceResult.Value
                : new();
        }

        internal async ValueTask<FuturesContractV3ReadModel> GetOnTheRunFuturesContractAsync(string symbol)
        {
            var entityId = new GetOnTheRunFuturesContractParameter(symbol);
            GetOnTheRunFuturesContractQuery query = new(symbol)
            {
                Subject = new ActorSubject(ActorType.Query, GetOnTheRunFuturesContractQuery.Actor, GetOnTheRunFuturesContractQuery.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = GetOnTheRunFuturesContractQuery.ErrorId,
                QueryParams = entityId.Format()
            };
            var serviceResult = await (context.RequestAsync<FuturesContractV3ReadModel, GetOnTheRunFuturesContractQuery>(query)).ConfigureAwait(false);
            return (serviceResult.Success && serviceResult.Value is not null)
                ? serviceResult.Value
                : new()!;
        }
    }

    extension(IEventActorContext commandApi)
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="context"></param>
        /// <param name="e"></param>
        /// <param name="futuresContract"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async ValueTask StartFuturesTickDataStreamingAsync(IEvent e, FuturesContractV3ReadModel futuresContract, FuturesDataId entityId)
        {
            ArgumentNullException.ThrowIfNull(commandApi);
            ArgumentNullException.ThrowIfNull(futuresContract);
            var (valueDate, resetStream) = e switch
            {
                MarketDataFeedStartedCompleteEvent o => (o.ValueDate, o.ResetStream),
                MarketDataFeedResetCompleteEvent o => (o.ValueDate, true),
                _ => throw new InvalidOperationException($"Unsupported event type: {e.GetType().FullName}")
            };
            var result = await (commandApi.StartFuturesTickDataStreamingAsync(
                futuresContract, valueDate, resetStream, entityId)).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"Futures tick streaming was not accepted for '{futuresContract.ContractId}': {result.ErrorMessage}");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="context"></param>
        /// <param name="e"></param>
        /// <param name="entityId"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async ValueTask StartFuturesBarDataStreamingAsync(IEvent e, FuturesBarDataStreamingId entityId)
        {
            ArgumentNullException.ThrowIfNull(commandApi);
            var (futuresContracts, valueDate) = e switch
            {
                MarketDataFeedStartedCompleteEvent o => (o.FuturesContracts!, o.ValueDate),
                MarketDataFeedResetCompleteEvent o => (o.FuturesContracts!, o.ValueDate),
                _ => throw new InvalidOperationException($"Unsupported event type: {e.GetType().FullName}")
            };
            var result = await (commandApi.StartFuturesBarDataStreamingAsync(
                futuresContracts, valueDate, entityId)).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"Futures bar streaming was not accepted for value date '{valueDate:yyyy-MM-dd}': {result.ErrorMessage}");
        }
    }

    extension(IEventActorContext context)
    {
        internal async ValueTask<OptionTradeReadModel> GetOptionTradeQueryAsync(int orderId, int tradeId)
        {
            var entityId = new GetOptionTradeParameter(orderId, tradeId);
            GetOptionTradeQuery query = new(orderId, tradeId)
            {
                Subject = new ActorSubject(ActorType.Query, GetOptionTradeQuery.Actor, GetOptionTradeQuery.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = GetOptionTradeQuery.ErrorId,
                QueryParams = entityId.Format()
            };
            var serviceResult = await (context.RequestAsync<OptionTradeReadModel, GetOptionTradeQuery>(query)).ConfigureAwait(false);
            return (serviceResult.Success && serviceResult.Value is not null)
                ? serviceResult.Value
                : new();
        }

        internal async ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractAsync(string contractId)
        {
            var entityId = new GetFuturesOptionContractParameter(contractId);
            GetFuturesOptionContractQuery query = new(contractId)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = GetFuturesOptionContractQuery.ErrorId,
                QueryParams = entityId.Format()
            };
            var serviceResult = await (context.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(query)).ConfigureAwait(false);
            return (serviceResult.Success && serviceResult.Value is not null)
                ? serviceResult.Value
                : new();
        }

        internal async ValueTask<FuturesContractV3ReadModel> GetFuturesContractAsync(string contractId)
        {
            var entityId = new GetFuturesContractParameter(contractId);
            GetFuturesContractQuery query = new(contractId)
            {
                Subject = new ActorSubject(ActorType.Query, GetFuturesContractQuery.Actor, GetFuturesContractQuery.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = GetFuturesContractQuery.ErrorId,
                QueryParams = entityId.Format()
            };
            var serviceResult = await (context.RequestAsync<FuturesContractV3ReadModel, GetFuturesContractQuery>(query)).ConfigureAwait(false);
            return (serviceResult.Success && serviceResult.Value is not null)
                ? serviceResult.Value
                : new();
        }
    }



}
