using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;

/// <summary>
/// Sends Market Data Feed commands from a running event actor and returns their typed replies.
/// </summary>
/// <remarks>
/// Operations construct strongly typed subjects and entity identifiers for feed lifecycle, streaming, and
/// persistence commands before dispatching them through the captured <see cref="IEventActorContext"/>.
/// Operations execute directly against the supplied event actor context.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class MarketDataFeedCommandApiExtensions
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3398", Justification = "This ordinary static helper is shared by C# 14 extension members; it is not a local function that can be moved into the lowered extension type.")]
    static ActorSubject Subject(string actor, string verb, IActorEntityId entityId)
        => new(ActorType.Command, actor, verb, entityId.Format());

    extension(IEventActorContext context)
    {

        /// <summary>
        /// Sends the turn trade live feed off command and awaits its typed actor reply.
        /// </summary>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="tradeId">The global Portfolio, Fund, Order, and Trade identity.</param>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> TurnTradeLiveFeedOffAsync(
            Guid commandId,
            TradeEntityId tradeId,
            DateOnly valueDate)
        {
            TurnTradeLiveFeedOffCommand command = new(tradeId, valueDate)
            {
                CommandId = commandId,
                Subject = Subject(
                    TurnTradeLiveFeedOffCommand.Actor,
                    TurnTradeLiveFeedOffCommand.Verb,
                    tradeId),
                EntityId = tradeId,
                ErrorCode = TurnTradeLiveFeedOffCommand.ErrorId
            };
            return context.RequestCommandAsync<TurnTradeLiveFeedOffCommand, TradeEntityId>(command);
        }

        /// <summary>
        /// Sends the turn trade live feed on command and awaits its typed actor reply.
        /// </summary>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="tradeId">The global Portfolio, Fund, Order, and Trade identity.</param>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> TurnTradeLiveFeedOnAsync(
            Guid commandId,
            TradeEntityId tradeId,
            DateOnly valueDate)
        {
            TurnTradeLiveFeedOnCommand command = new(tradeId, valueDate)
            {
                CommandId = commandId,
                Subject = Subject(
                    TurnTradeLiveFeedOnCommand.Actor,
                    TurnTradeLiveFeedOnCommand.Verb,
                    tradeId),
                EntityId = tradeId,
                ErrorCode = TurnTradeLiveFeedOnCommand.ErrorId
            };
            return context.RequestCommandAsync<TurnTradeLiveFeedOnCommand, TradeEntityId>(command);
        }

        /// <summary>
        /// Sends the stop futures bar data streaming command and awaits its typed actor reply.
        /// </summary>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> StopFuturesBarDataStreamingAsync(DateOnly valueDate)
        {
            var entityId = new FuturesBarDataStreamingId(valueDate);
            StopFuturesBarDataStreamingCommand command = new(valueDate)
            {
                Subject = Subject(
                    StopFuturesBarDataStreamingCommand.Actor,
                    StopFuturesBarDataStreamingCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = StopFuturesBarDataStreamingCommand.ErrorId
            };
            return context.RequestCommandAsync<StopFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(command);
        }

        /// <summary>
        /// Sends the stop futures option tick data streaming command and awaits its typed actor reply.
        /// </summary>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="entityId">The target actor entity identifier.</param>
        /// <param name="contractId">The contract identifier.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> StopFuturesOptionTickDataStreamingAsync(
            Guid commandId,
            FuturesOptionTickEntityId entityId,
            string contractId)
        {
            ArgumentNullException.ThrowIfNull(entityId);
            StopFuturesOptionTickDataStreamingCommand command = new(entityId, contractId)
            {
                CommandId = commandId,
                Subject = Subject(
                    StopFuturesOptionTickDataStreamingCommand.Actor,
                    StopFuturesOptionTickDataStreamingCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = StopFuturesOptionTickDataStreamingCommand.ErrorId
            };
            return context.RequestCommandAsync<StopFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(command);
        }

        /// <summary>
        /// Sends the start futures tick data streaming command and awaits its typed actor reply.
        /// </summary>
        /// <param name="futuresContract">The futures contract associated with the operation.</param>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <param name="resetStream">Whether the existing stream should be reset.</param>
        /// <param name="entityId">The target actor entity identifier.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> StartFuturesTickDataStreamingAsync(
            FuturesContractV3ReadModel futuresContract,
            DateOnly valueDate,
            bool resetStream,
            FuturesDataId entityId)
        {
            ArgumentNullException.ThrowIfNull(entityId);
            StartFuturesTickDataStreamingCommand command = new(futuresContract, valueDate, resetStream)
            {
                Subject = Subject(
                    StartFuturesTickDataStreamingCommand.Actor,
                    StartFuturesTickDataStreamingCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = StartFuturesTickDataStreamingCommand.ErrorId
            };
            return context.RequestCommandAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(command);
        }

        /// <summary>
        /// Sends the start futures bar data streaming command and awaits its typed actor reply.
        /// </summary>
        /// <param name="futuresContracts">The futures contracts associated with the operation.</param>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <param name="entityId">The target actor entity identifier.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> StartFuturesBarDataStreamingAsync(
            FuturesContractV3ReadModel[] futuresContracts,
            DateOnly valueDate,
            FuturesBarDataStreamingId entityId)
        {
            ArgumentNullException.ThrowIfNull(entityId);
            StartFuturesBarDataStreamingCommand command = new(futuresContracts, valueDate)
            {
                Subject = Subject(
                    StartFuturesBarDataStreamingCommand.Actor,
                    StartFuturesBarDataStreamingCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = StartFuturesBarDataStreamingCommand.ErrorId
            };
            return context.RequestCommandAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(command);
        }

        /// <summary>
        /// Sends the start futures option tick data streaming command and awaits its typed actor reply.
        /// </summary>
        /// <param name="commandId">The originating command identifier.</param>
        /// <param name="entityId">The target actor entity identifier.</param>
        /// <param name="contract">The futures-option contract.</param>
        /// <param name="baseContract">The underlying futures contract.</param>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <param name="maturityDate">The option maturity date.</param>
        /// <param name="riskFreeRate">The annualized risk-free rate.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> StartFuturesOptionTickDataStreamingAsync(
            Guid commandId,
            FuturesOptionTickEntityId entityId,
            FuturesOptionContractReadModel contract,
            FuturesContractV3ReadModel baseContract,
            DateOnly valueDate,
            DateOnly maturityDate,
            double riskFreeRate)
        {
            ArgumentNullException.ThrowIfNull(entityId);
            StartFuturesOptionTickDataStreamingCommand command = new(
                entityId,
                contract,
                baseContract,
                valueDate,
                maturityDate,
                riskFreeRate)
            {
                CommandId = commandId,
                Subject = Subject(
                    StartFuturesOptionTickDataStreamingCommand.Actor,
                    StartFuturesOptionTickDataStreamingCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = StartFuturesOptionTickDataStreamingCommand.ErrorId
            };
            return context.RequestCommandAsync<StartFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(command);
        }

        /// <summary>
        /// Sends the insert futures bar data command and awaits its typed actor reply.
        /// </summary>
        /// <param name="futuresBarData">The futures bar data to persist.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> InsertFuturesBarDataAsync(FuturesBarDataReadModel futuresBarData)
        {
            ArgumentNullException.ThrowIfNull(futuresBarData);
            InsertFuturesBarDataCommand command = new(futuresBarData)
            {
                CommandId = Guid.NewGuid(),
                Subject = Subject(
                    InsertFuturesBarDataCommand.Actor,
                    InsertFuturesBarDataCommand.Verb,
                    futuresBarData.Id),
                EntityId = futuresBarData.Id,
                ErrorCode = InsertFuturesBarDataCommand.ErrorId
            };
            return context.RequestCommandAsync<InsertFuturesBarDataCommand, FuturesBarDataId>(command);
        }

        /// <summary>
        /// Sends the delete streaming request ID command and awaits its typed actor reply.
        /// </summary>
        /// <param name="feedId">The streaming feed identifier.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> DeleteStreamingRequestIdAsync(FeedId feedId)
        {
            ArgumentNullException.ThrowIfNull(feedId);
            DeleteStreamingRequestIdCommand command = new(feedId)
            {
                Subject = Subject(
                    DeleteStreamingRequestIdCommand.Actor,
                    DeleteStreamingRequestIdCommand.Verb,
                    feedId),
                EntityId = feedId,
                ErrorCode = DeleteStreamingRequestIdCommand.ErrorId
            };
            return context.RequestCommandAsync<DeleteStreamingRequestIdCommand, FeedId>(command);
        }

        /// <summary>
        /// Sends the insert futures EOD data command and awaits its typed actor reply.
        /// </summary>
        /// <param name="valueDate">The applicable market value date.</param>
        /// <param name="futuresTickData">The futures tick data used by the operation.</param>
        /// <param name="futuresContract">The futures contract associated with the operation.</param>
        /// <param name="eodDataToday">The current EOD data.</param>
        /// <param name="eodDataRange">The historical EOD data used by the calculation.</param>
        /// <param name="normalCurveData">The normal-curve lookup data.</param>
        /// <param name="windowSize">The calculation window size.</param>
        /// <param name="vixEodData">The VIX EOD data used by the calculation.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> InsertFuturesEodDataAsync(
            DateOnly valueDate,
            FuturesTickDataV2ReadModel futuresTickData,
            FuturesContractV3ReadModel futuresContract,
            FuturesEodDataV2ReadModel eodDataToday,
            ICollection<FuturesEodDataV2ReadModel> eodDataRange,
            NormalCurveTableReadModel normalCurveData,
            int windowSize,
            ICollection<VixFuturesEodDataReadModel> vixEodData)
        {
            ArgumentNullException.ThrowIfNull(futuresContract);
            var entityId = new FuturesEodDataId(futuresContract.ContractId, valueDate);
            InsertFuturesEodDataCommand command = new(
                valueDate,
                futuresTickData,
                futuresContract,
                eodDataToday,
                eodDataRange,
                normalCurveData,
                windowSize,
                vixEodData)
            {
                Subject = Subject(
                    InsertFuturesEodDataCommand.Actor,
                    InsertFuturesEodDataCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = InsertFuturesEodDataCommand.ErrorId
            };
            return context.RequestCommandAsync<InsertFuturesEodDataCommand, FuturesEodDataId>(command);
        }

        /// <summary>
        /// Sends the insert VIX futures EOD data command and awaits its typed actor reply.
        /// </summary>
        /// <param name="futuresTickData">The futures tick data used by the operation.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> InsertVixFuturesEodDataAsync(
            FuturesTickDataV2ReadModel futuresTickData)
        {
            ArgumentNullException.ThrowIfNull(futuresTickData);
            var entityId = new FuturesEodDataId(futuresTickData.ContractId, futuresTickData.ValueDate);
            InsertVixFuturesEodDataCommand command = new(futuresTickData)
            {
                Subject = Subject(
                    InsertVixFuturesEodDataCommand.Actor,
                    InsertVixFuturesEodDataCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = InsertVixFuturesEodDataCommand.ErrorId
            };
            return context.RequestCommandAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(command);
        }

        /// <summary>
        /// Sends the insert futures tick data command and awaits its typed actor reply.
        /// </summary>
        /// <param name="futuresContract">The futures contract associated with the operation.</param>
        /// <param name="futuresTickData">The futures tick data used by the operation.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> InsertFuturesTickDataAsync(
            FuturesContractV3ReadModel futuresContract,
            FuturesTickDataV2ReadModel futuresTickData)
        {
            ArgumentNullException.ThrowIfNull(futuresContract);
            ArgumentNullException.ThrowIfNull(futuresTickData);
            var entityId = new FuturesDataId(futuresContract.ContractId, futuresTickData.ValueDate);
            InsertFuturesTickDataCommand command = new(futuresContract, futuresTickData)
            {
                Subject = Subject(
                    InsertFuturesTickDataCommand.Actor,
                    InsertFuturesTickDataCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = InsertFuturesTickDataCommand.ErrorId
            };
            return context.RequestCommandAsync<InsertFuturesTickDataCommand, FuturesDataId>(command);
        }

        /// <summary>
        /// Sends the insert futures option tick price data command and awaits its typed actor reply.
        /// </summary>
        /// <param name="underlyingContract">The underlying futures contract.</param>
        /// <param name="optionContract">The futures-option tick data.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionTickPriceDataAsync(
            FuturesContractV3ReadModel underlyingContract,
            FuturesOptionTickDataV2ReadModel optionContract)
        {
            ArgumentNullException.ThrowIfNull(optionContract);
            var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, optionContract.ValueDate);
            InsertFuturesOptionTickPriceDataCommand command = new(underlyingContract, optionContract)
            {
                Subject = Subject(
                    InsertFuturesOptionTickPriceDataCommand.Actor,
                    InsertFuturesOptionTickPriceDataCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = InsertFuturesOptionTickPriceDataCommand.ErrorId
            };
            return context.RequestCommandAsync<InsertFuturesOptionTickPriceDataCommand, FuturesOptionTickEntityId>(command);
        }

        /// <summary>
        /// Sends the insert futures option tick data command and awaits its typed actor reply.
        /// </summary>
        /// <param name="underlyingContract">The underlying futures contract.</param>
        /// <param name="optionContract">The futures-option tick data.</param>
        /// <returns>A value task containing the typed command result returned by the target actor.</returns>
        public ValueTask<ServiceResult<GuidResult>> InsertFuturesOptionTickDataAsync(
            FuturesContractV3ReadModel underlyingContract,
            FuturesOptionTickDataV2ReadModel optionContract)
        {
            ArgumentNullException.ThrowIfNull(optionContract);
            var entityId = new FuturesOptionTickEntityId(optionContract.ContractId, optionContract.ValueDate);
            InsertFuturesOptionTickDataCommand command = new(underlyingContract, optionContract)
            {
                Subject = Subject(
                    InsertFuturesOptionTickDataCommand.Actor,
                    InsertFuturesOptionTickDataCommand.Verb,
                    entityId),
                EntityId = entityId,
                ErrorCode = InsertFuturesOptionTickDataCommand.ErrorId
            };
            return context.RequestCommandAsync<InsertFuturesOptionTickDataCommand, FuturesOptionTickEntityId>(command);
        }

        async ValueTask<ServiceResult<GuidResult>> RequestCommandAsync<TCommand, TEntityId>(TCommand command)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId
        {
            if (command.CommandId == Guid.Empty)
            {
                EventInitHelper.SetProperty(
                    command,
                    nameof(ICommand.CommandId),
                    Guid.NewGuid());
            }
            command.CheckForEmptyCommandId();

            var result = await (context.RequestAsync<TCommand, TEntityId>(command)).ConfigureAwait(false);
            if (result?.Success != true)
                throw new InvalidOperationException(result?.ErrorMessage);
            return result;
        }

    }
}
