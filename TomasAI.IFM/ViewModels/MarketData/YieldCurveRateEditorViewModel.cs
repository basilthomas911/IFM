using System;
using System.Collections.Generic;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Extensions;
using RestSharp.Validation;

namespace TomasAI.IFM.ViewModels.MarketData
{
    public class YieldCurveRateEditorViewModel : BaseEditorViewModel
    {
        List<YieldCurveRateReadModel> _yieldCurveRates;
        Guid _commandId;
        Action<Guid> _setCommandId;
        ICollection<IEvent> _consumeEvents;
        MarketDataEventModel _eventModel;
        MarketDataCommandModel _commandModel;
        MarketDataQueryModel _queryModel;

        public YieldCurveRateEditorViewModel(IAppRoot appRoot) : base(appRoot)
        {
            _yieldCurveRates = new List<YieldCurveRateReadModel>();
            _commandId = Guid.Empty;
            _eventModel = AppRoot.GetModel<MarketDataEventModel>();
            _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
            _commandModel.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
            _queryModel.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            _setCommandId = commandId => _commandId = commandId;
            _consumeEvents = new IEvent[]{
                new YieldCurveRateAddedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRateAddedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRateChangedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRateChangedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRateRemovedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRateRemovedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRatesImportedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
                new YieldCurveRatesImportedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            };
        }

        public bool CanChangeRemove { get; set; }

        public bool CanImport => true;

        public Action<bool> OnDataLoaded;
        public Action<bool> OnChangeAction;
        public Action<string[]> OnYieldCurveRateTimePeriodsLoaded;
        public Action<YieldCurveRateReadModel[]> OnYieldCurveRatesLoaded;
        public Action<YieldCurveRateReadModel> OnYieldCurveRateAdded;
        public Action<bool> OnAddAction;
        public Action<DateTime> OnYieldCurveRateRemoved;
        public Action<YieldCurveRateReadModel> OnYieldCurveRateChanged;
        public Action<int> OnYieldCurveRatesImported;
        public Action OnShowYieldCurveRates;

        /// <summary>
        /// start listening for yield curve rate editor events
        /// </summary>
        public void StartListener()
            => _eventModel.Execute(async e => {
                await e.StartMarketDataListenerAsync(_consumeEvents, HandleEventAsync);
            });

        /// <summary>
        /// stop listening for yield curve rate editor events
        /// </summary>
        public void StopListener()
            => _eventModel.Execute(async e => {
                await e.StopMarketDataListenerAsync();
            });

        /// <summary>
        /// exute yield curve rate editor event actions
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        Task HandleEventAsync(IEvent e)
        {
            if (_commandId != e.CommandId)
                return Task.CompletedTask;
            return e switch
            {
                YieldCurveRateAddedCompleteEvent o => AddYieldCurveRateCompleted(o),
                YieldCurveRateAddedFailEvent o => AddYieldCurveRateFailed(o),
                YieldCurveRateChangedCompleteEvent o => ChangeYieldCurveRateCompleted(o),
                YieldCurveRateChangedFailEvent o => ChangeYieldCurveRateFailed(o),
                YieldCurveRateRemovedCompleteEvent o => RemoveYieldCurveRateCompleted(o),
                YieldCurveRateRemovedFailEvent o => RemoveYieldCurveRateFailed(o),
                YieldCurveRatesImportedCompleteEvent o => ImportYieldCurveRatesCompleted(o),
                YieldCurveRatesImportedFailEvent o => ImportYieldCurveRatesFailed(o),
                _ => Task.CompletedTask
            };
        }

        /// <summary>
        /// add yield curve rate
        /// </summary>
        /// <param name="yieldCurveRate"></param>
        public void AddYieldCurveRate(YieldCurveRateReadModel yieldCurveRate)
        {
            IsArgumentNull.Check(yieldCurveRate);

            _commandModel.Execute(async model => {
                await model.AddYieldCurveRateAsync(yieldCurveRate, _setCommandId);
            });
        }

        Task AddYieldCurveRateCompleted(YieldCurveRateAddedCompleteEvent e)
        {
            RefreshUpdatedYieldCurveRates($"Yield Curve Rate: {e.YieldCurveRate.ValueDate:yyyy-MM-dd} Added");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task AddYieldCurveRateFailed(YieldCurveRateAddedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rate Add failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// change yield curve rate
        /// </summary>
        /// <param name="yieldCurveRate"></param>
        public void ChangeYieldCurveRate(YieldCurveRateReadModel yieldCurveRate)
        {
            IsArgumentNull.Check(yieldCurveRate);

            _commandModel.Execute(async model => {
                await model.ChangeYieldCurveRateAsync(yieldCurveRate, _setCommandId);
            });
        }

        Task ChangeYieldCurveRateCompleted(YieldCurveRateChangedCompleteEvent e)
        {
            RefreshUpdatedYieldCurveRates($"Yield Curve Rate: {e.YieldCurveRate.ValueDate:yyyy-MM-dd} Changed");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task ChangeYieldCurveRateFailed(YieldCurveRateChangedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rate Change failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// remove yield curve rate
        /// </summary>
        /// <param name="valueDate"></param>
        public void RemoveYieldCurveRate(DateTime valueDate)
        {
            IsArgumentNull.Check(valueDate);

            _commandModel.Execute(async model => {
                await model.RemoveYieldCurveRateAsync(valueDate, _setCommandId);
            });
        }

        Task RemoveYieldCurveRateCompleted(YieldCurveRateRemovedCompleteEvent e)
        {
            RefreshUpdatedYieldCurveRates($"Yield Curve Rate: {e.ValueDate:yyyy-MM-dd} Removed");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task RemoveYieldCurveRateFailed(YieldCurveRateRemovedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rate Remove failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        /// <summary>
        /// import yiele curve rates
        /// </summary>
        /// <param name="importDate"></param>
        public void ImportYieldCurveRates(DateTime importDate)
        {
            IsArgumentNull.Check(importDate);

            _commandModel.Execute(async model => {
                var yieldCurveRates = default(YieldCurveRateReadModel[]);
                await _queryModel.GetExternalYieldCurveRatesAsync(e => yieldCurveRates = e);
                await model.ImportYieldCurveRatesAsync(importDate, yieldCurveRates, _setCommandId);
            });
        }

        Task ImportYieldCurveRatesCompleted(YieldCurveRatesImportedCompleteEvent e)
        {
            RefreshUpdatedYieldCurveRates($"{e.YieldCurveRates.Length} Yield Curve Rates Imported on: {e.ImportDate:yyyy-MM-dd}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        Task ImportYieldCurveRatesFailed(YieldCurveRatesImportedFailEvent e)
        {
            WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rates Import failed due to: {e.ErrorMessage}");
            _commandId = Guid.Empty;
            return Task.CompletedTask;
        }

        public void LoadYieldCurveRateTimePeriods()
             => _queryModel.Execute(async model => {
                 await model.GetYieldCurveRateTimePeriodsAsync(timePeriods => OnYieldCurveRateTimePeriodsLoaded(timePeriods));
             });

        public void LoadYieldCurveRates(DateTime startDate, DateTime endDate)
            => _queryModel.Execute(async model => {
                await model.GetYieldCurveRatesAsync(startDate, endDate, yieldCurveRates => OnYieldCurveRatesLoaded(yieldCurveRates));
            });

        public YieldCurveRateReadModel GetYieldCurveRate(int index) => _yieldCurveRates[index];

        public void SetYieldCurveRates(YieldCurveRateReadModel[] yieldCurveRates)
        {
            if (yieldCurveRates is not null)
                _yieldCurveRates = new List<YieldCurveRateReadModel>(yieldCurveRates);
        }

        void RefreshUpdatedYieldCurveRates(string consoleStatus)
        {
            OnShowYieldCurveRates?.Invoke();
            LoadYieldCurveRateTimePeriods();
            WriteStatusConsole(LogSourceType.MarketData, consoleStatus);
        }

    }
}
