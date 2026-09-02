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

namespace TomasAI.IFM.UI.Net.Services.Analytics
{
    /// <summary>Defines values used by the IntradaySignalType UI service workflow.</summary>
    public enum IntradaySignalType
    {
        Rsi,
        Atr,
        Adx,
        Macd
    }

    /// <summary>Represents IntradaySignalLifecycleStatus state returned by a UI service.</summary>
    public sealed record IntradaySignalLifecycleStatus(
        IntradaySignalType SignalType,
        TimeFrameType TimeFrame,
        string EntityId,
        bool Success,
        Guid CommandId,
        int ErrorCode,
        string ErrorMessage);

    /// <summary>Represents IntradaySignalLifecycleResult state returned by a UI service.</summary>
    public sealed record IntradaySignalLifecycleResult(
        IReadOnlyList<IntradaySignalLifecycleStatus> Signals)
    {
        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public int RequestedCount => Signals.Count;
        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public int SuccessfulCount => Signals.Count(signal => signal.Success);
        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public bool AllSucceeded => RequestedCount > 0 && SuccessfulCount == RequestedCount;
        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public IReadOnlyList<IntradaySignalLifecycleStatus> Failures
            => Signals.Where(signal => !signal.Success).ToArray();
    }

    /// <summary>Provides the MarketDataAnalyticsCommandService UI service boundary.</summary>
    public class MarketDataAnalyticsCommandService : UiServiceBase<MarketDataAnalyticsCommandService>
    {
        readonly IMarketDataAnalyticsCommandApi _commandApi;
        readonly IMarketOutlookUIEventConsumer _marketOutlookEventConsumer;
        readonly IFuturesRsiSignalUIEventConsumer _futuresRsiSignalEventConsumer;

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public MarketDataAnalyticsCommandService(
            IMarketDataAnalyticsCommandApi commandApi,
            IFuturesRsiSignalUIEventConsumer futuresRsiSignalEventConsumer,
            IMarketOutlookUIEventConsumer marketOutlookEventConsumer)
        {
            _commandApi = commandApi;
            _futuresRsiSignalEventConsumer = futuresRsiSignalEventConsumer;
            _marketOutlookEventConsumer = marketOutlookEventConsumer;
        }

        /// <summary>Requests the Development-only historical Analytics startup warm-up.</summary>
        public Task<ServiceResult<Guid>> EnsureHistoricalAnalyticsWarmupAsync(
            DateOnly candidateValueDate,
            string analyticsTargetContractId)
            => _commandApi.EnsureHistoricalAnalyticsWarmupAsync(candidateValueDate, analyticsTargetContractId);

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

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public async Task StartMarketOutlookEventConsumerAsync(
            Guid siteId,
            Action<MarketOutlookSnapshotInsertedEvent> listenerAction)
            => await _marketOutlookEventConsumer.StartAsync(siteId, listenerAction);

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public async Task StopMarketOutlookEventConsumerAsync(Guid siteId)
            => await _marketOutlookEventConsumer.StopAsync(siteId);

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
