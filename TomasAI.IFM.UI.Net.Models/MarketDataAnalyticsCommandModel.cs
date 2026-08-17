using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Models
{
    public enum IntradaySignalType
    {
        Rsi,
        Atr,
        Adx,
        Macd
    }

    public sealed record IntradaySignalLifecycleStatus(
        IntradaySignalType SignalType,
        TimeFrameType TimeFrame,
        string EntityId,
        bool Success,
        Guid CommandId,
        int ErrorCode,
        string ErrorMessage);

    public sealed record IntradaySignalLifecycleResult(
        IReadOnlyList<IntradaySignalLifecycleStatus> Signals)
    {
        public int RequestedCount => Signals.Count;
        public int SuccessfulCount => Signals.Count(signal => signal.Success);
        public bool AllSucceeded => RequestedCount > 0 && SuccessfulCount == RequestedCount;
        public IReadOnlyList<IntradaySignalLifecycleStatus> Failures
            => Signals.Where(signal => !signal.Success).ToArray();
    }

    public class MarketDataAnalyticsCommandModel : BaseModel<MarketDataAnalyticsCommandModel>
    {
        readonly IMarketDataAnalyticsCommandApi _commandApi;
        readonly IFuturesTradeSignalUIEventConsumer _futuresTradeSignalEventConsumer;
        readonly IFuturesRsiSignalUIEventConsumer _futuresRsiSignalEventConsumer;

        public MarketDataAnalyticsCommandModel(
            IMarketDataAnalyticsCommandApi commandApi,
            IFuturesTradeSignalUIEventConsumer futuresTradeSignalEventConsumer,
            IFuturesRsiSignalUIEventConsumer futuresRsiSignalEventConsumer)
        {
            _commandApi = commandApi;
            _futuresTradeSignalEventConsumer = futuresTradeSignalEventConsumer;
            _futuresRsiSignalEventConsumer = futuresRsiSignalEventConsumer;
        }

        /// <summary>
        /// start futures rsi signal service
        /// </summary>
        /// <param name="futuresRsiSignalId"></param>
        /// <returns></returns>
        public async Task StartFuturesRsiSignalServiceAsync(FuturesRsiSignalEntityId entityId)
            => await _commandApi.StartFuturesRsiSignalAsync(entityId);

        /// <summary>
        /// stop futures rsi signal service
        /// </summary>
        /// <param name="futuresRsiSignalId"></param>
        /// <returns></returns>
        public async Task StopFuturesRsiSignalServiceAsync(FuturesRsiSignalEntityId entityId)
            => await _commandApi.StopFuturesRsiSignalAsync(entityId);

        /// <summary>
        /// Starts RSI, ATR, ADX, and MACD for every timeframe in the authoritative intraday profile.
        /// Each actor is attempted once; failures are returned to the caller without retry.
        /// </summary>
        public Task<IntradaySignalLifecycleResult> StartFuturesIntradaySignalsAsync(
            string contractId,
            DateOnly valueDate,
            CancellationToken cancellationToken = default)
            => ExecuteIntradayLifecycleAsync(contractId, valueDate, start: true, cancellationToken);

        /// <summary>Stops every actor in the authoritative intraday profile.</summary>
        public Task<IntradaySignalLifecycleResult> StopFuturesIntradaySignalsAsync(
            string contractId,
            DateOnly valueDate,
            CancellationToken cancellationToken = default)
            => ExecuteIntradayLifecycleAsync(contractId, valueDate, start: false, cancellationToken);

        async Task<IntradaySignalLifecycleResult> ExecuteIntradayLifecycleAsync(
            string contractId,
            DateOnly valueDate,
            bool start,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = FuturesIntradaySignalActivationProfile.Create(contractId, valueDate);
            var operations = profile.SelectMany(activation => new[]
            {
                ExecuteLifecycleCommandAsync(
                    IntradaySignalType.Rsi,
                    activation.TimeFrame,
                    activation.Rsi.Format(),
                    () => start
                        ? _commandApi.StartFuturesRsiSignalAsync(activation.Rsi)
                        : _commandApi.StopFuturesRsiSignalAsync(activation.Rsi)),
                ExecuteLifecycleCommandAsync(
                    IntradaySignalType.Atr,
                    activation.TimeFrame,
                    activation.Atr.Format(),
                    () => start
                        ? _commandApi.StartFuturesAtrSignalAsync(activation.Atr)
                        : _commandApi.StopFuturesAtrSignalAsync(activation.Atr)),
                ExecuteLifecycleCommandAsync(
                    IntradaySignalType.Adx,
                    activation.TimeFrame,
                    activation.Adx.Format(),
                    () => start
                        ? _commandApi.StartFuturesAdxSignalAsync(activation.Adx)
                        : _commandApi.StopFuturesAdxSignalAsync(activation.Adx)),
                ExecuteLifecycleCommandAsync(
                    IntradaySignalType.Macd,
                    activation.TimeFrame,
                    activation.Macd.Format(),
                    () => start
                        ? _commandApi.StartFuturesMacdSignalAsync(activation.Macd)
                        : _commandApi.StopFuturesMacdSignalAsync(activation.Macd))
            }).ToArray();

            return new IntradaySignalLifecycleResult(await Task.WhenAll(operations));
        }

        static async Task<IntradaySignalLifecycleStatus> ExecuteLifecycleCommandAsync(
            IntradaySignalType signalType,
            TimeFrameType timeFrame,
            string entityId,
            Func<Task<ServiceResult<Guid>>> command)
        {
            try
            {
                var result = await command().ConfigureAwait(false);
                return new IntradaySignalLifecycleStatus(
                    signalType,
                    timeFrame,
                    entityId,
                    result?.Success == true,
                    result?.Value ?? Guid.Empty,
                    result?.ErrorCode ?? -1,
                    result?.ErrorMessage ?? "The signal command returned no result.");
            }
            catch (Exception exception)
            {
                return new IntradaySignalLifecycleStatus(
                    signalType,
                    timeFrame,
                    entityId,
                    false,
                    Guid.Empty,
                    -1,
                    exception.Message);
            }
        }

        /// <summary>
        /// start listening for futures trade signal updates
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesTradeSignalEventConsumerAsync(Guid siteId, Action<FuturesTradeSignalUpdatedNotifyEvent> listenerAction)
            => await _futuresTradeSignalEventConsumer.StartAsync(siteId, listenerAction);

        /// <summary>
        /// stop listening for futures trade signal updates
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesTradeSignalEventConsumerAsync(Guid siteId)
            => await _futuresTradeSignalEventConsumer.StopAsync(siteId);

        /// <summary>
        /// start listening for generated futures rsi signals
        /// </summary>
        /// <param name="siteId"></param>
        /// <param name="listenerAction"></param>
        public async Task StartFuturesRsiSignalEventConsumerAsync(Guid siteId, Action<FuturesTdiSignalGeneratedCompleteEvent> listenerAction)
            => await _futuresRsiSignalEventConsumer.StartAsync(listenerAction);

        /// <summary>
        /// stop listening for generated futures rsi signals
        /// </summary>
        /// <param name="siteId"></param>
        public async Task StopFuturesRsiSignalEventConsumerAsync(Guid siteId)
            => await _futuresRsiSignalEventConsumer.StopAsync();
    }
}
