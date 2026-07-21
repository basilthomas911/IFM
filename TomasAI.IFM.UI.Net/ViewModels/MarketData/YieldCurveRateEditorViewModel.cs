using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Represents the view model for managing yield curve rates in the editor.
/// </summary>
/// <remarks>This class provides functionality to add, update, remove, and import yield curve rates, as well as to
/// load yield curve rate data for specific time periods or date ranges. It also manages event handling for operations
/// related to yield curve rates, such as completion or failure events.</remarks>
public class YieldCurveRateEditorViewModel : BaseEditorViewModel
{
    List<YieldCurveRateReadModel> _yieldCurveRates = [];
    Guid _commandId;
    Action<Guid> _setCommandId;
    readonly ICollection<IEvent> _consumeEvents = [];
    readonly MarketDataEventModel?_eventModel;
    readonly MarketDataCommandModel? _commandModel;
    readonly MarketDataQueryModel? _queryModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="YieldCurveRateEditorViewModel"/> class.
    /// </summary>
    /// <remarks>This constructor sets up the view model by initializing dependencies and subscribing to error
    /// handling  and event consumption for market data operations. The provided <paramref name="appRoot"/> is used to 
    /// retrieve required models such as <see cref="MarketDataEventModel"/>, <see cref="MarketDataCommandModel"/>,  and
    /// <see cref="MarketDataQueryModel"/>.</remarks>
    /// <param name="appRoot">The application root object that provides access to application-wide services and models.</param>
    public YieldCurveRateEditorViewModel(IAppRoot appRoot) 
        : base(appRoot)
    {
        _yieldCurveRates = [];
        _commandId = Guid.Empty;
        _eventModel = AppRoot.GetModel<MarketDataEventModel>();
        _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
        _commandModel.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
        _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
        _queryModel.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
        _setCommandId = commandId => _commandId = commandId;
        _consumeEvents = [
            new YieldCurveRateAddedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateAddedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateChangedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateChangedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateRemovedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateRemovedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRatesImportedCompleteEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRatesImportedFailEvent{ }.SetEventSource($"{EventTopic.MarketDataEvents}"),
        ];
    }

    public bool CanChangeRemove { get; set; }

    public bool CanImport 
        => true;

    public Action<bool> OnDataLoaded = (loaded) => { };
    public Action<bool> OnChangeAction = (changed) => { };
    public Action<string[]> OnYieldCurveRateTimePeriodsLoaded = (timePeriods) => { };
    public Action<YieldCurveRateReadModel[]> OnYieldCurveRatesLoaded = (rates) => { };
    public Action<YieldCurveRateReadModel> OnYieldCurveRateAdded = (rate) => { };
    public Action<bool> OnAddAction = (canAdd) => { };
    public Action<DateOnly> OnYieldCurveRateRemoved = (date) => { };
    public Action<YieldCurveRateReadModel> OnYieldCurveRateChanged = (rate) => { };
    public Action<int> OnYieldCurveRatesImported = (count) => { };
    public Action OnShowYieldCurveRates = () => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    /// <summary>
    /// start listening for yield curve rate editor events
    /// </summary>
    public void StartListener()
        => _eventModel?.Execute(async e => {
            await e.StartMarketDataListenerAsync(_consumeEvents, HandleEventAsync);
        });

    /// <summary>
    /// stop listening for yield curve rate editor events
    /// </summary>
    public void StopListener()
        => _eventModel?.Execute(async e => {
            await e.StopMarketDataListenerAsync();
        });

    /// <summary>
    /// exute yield curve rate editor event actions
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    ValueTask HandleEventAsync(IEvent e)
        => (_commandId != e.CommandId)
            ? ValueTask.CompletedTask
            : e switch
            {
                YieldCurveRateAddedCompleteEvent o => AddYieldCurveRateCompleted(o),
                YieldCurveRateAddedFailEvent o => AddYieldCurveRateFailed(o),
                YieldCurveRateChangedCompleteEvent o => ChangeYieldCurveRateCompleted(o),
                YieldCurveRateChangedFailEvent o => ChangeYieldCurveRateFailed(o),
                YieldCurveRateRemovedCompleteEvent o => RemoveYieldCurveRateCompleted(o),
                YieldCurveRateRemovedFailEvent o => RemoveYieldCurveRateFailed(o),
                YieldCurveRatesImportedCompleteEvent o => ImportYieldCurveRatesCompleted(o),
                YieldCurveRatesImportedFailEvent o => ImportYieldCurveRatesFailed(o),
                _ => ValueTask.CompletedTask
            };

    /// <summary>
    /// add yield curve rate
    /// </summary>
    /// <param name="yieldCurveRate"></param>
    /// <param name="overwrite">Indicates whether to overwrite an existing yield curve rate with the same value date.</param>
    public void AddYieldCurveRate(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
        => _commandModel?.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            IsArgumentNull.Check(yieldCurveRate);
            OnWaitCursor?.Invoke(); // Show wait cursor before executing the command
            WriteStatusConsole(LogSourceType.MarketData, $"Adding Yield Curve Rate: {yieldCurveRate.ValueDate:yyyy-MM-dd}...please wait");
            await model.AddYieldCurveRateAsync(yieldCurveRate, overwrite);
            OnYieldCurveRateAdded?.Invoke(yieldCurveRate);
            OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        });

    /// <summary>
    /// Handles the completion of adding a yield curve rate.
    /// </summary>
    /// <remarks>This method refreshes the updated yield curve rates and resets the command identifier after a
    /// yield curve rate has been successfully added.</remarks>
    /// <param name="e">An event containing details about the added yield curve rate, including its value date.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask AddYieldCurveRateCompleted(YieldCurveRateAddedCompleteEvent e)
    {
        RefreshUpdatedYieldCurveRates($"Yield Curve Rate: {e.YieldCurveRate.ValueDate:yyyy-MM-dd} Added");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles the failure event for adding a yield curve rate.
    /// </summary>
    /// <remarks>This method logs the failure details and resets the internal command identifier.</remarks>
    /// <param name="e">The event containing details about the failure, including the error code and message.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask AddYieldCurveRateFailed(YieldCurveRateAddedFailEvent e)
    {
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rate Add failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Updates the yield curve rate with the specified data and optionally overwrites existing values.
    /// </summary>
    /// <remarks>This method executes the update operation asynchronously and may trigger a wait cursor during
    /// execution.</remarks>
    /// <param name="yieldCurveRate">The yield curve rate data to be updated. Cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite existing yield curve rate data.  <see langword="true"/> to overwrite;
    /// otherwise, <see langword="false"/>.</param>
    public void ChangeYieldCurveRate(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
        => _commandModel?.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            IsArgumentNull.Check(yieldCurveRate);
            OnWaitCursor?.Invoke(); // Show wait cursor before executing the command
            WriteStatusConsole(LogSourceType.MarketData, $"Changing Yield Curve Rate: {yieldCurveRate.ValueDate:yyyy-MM-dd}...please wait");
            await model.ChangeYieldCurveRateAsync(yieldCurveRate, overwrite);
            OnYieldCurveRateChanged?.Invoke(yieldCurveRate);
            OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        });

    /// <summary>
    /// Handles the completion of a yield curve rate change operation.
    /// </summary>
    /// <remarks>This method refreshes the updated yield curve rates and resets the command state.  It also
    /// invokes the <see cref="OnDefaultCursor"/> action to reset the cursor to its default state.</remarks>
    /// <param name="e">An event containing details about the completed yield curve rate change, including the updated rate and its
    /// value date.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask ChangeYieldCurveRateCompleted(YieldCurveRateChangedCompleteEvent e)
    {
        RefreshUpdatedYieldCurveRates($"Yield Curve Rate: {e.YieldCurveRate.ValueDate:yyyy-MM-dd} Changed");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        WriteStatusConsole(LogSourceType.MarketData, $"Changed Yield Curve Rate: {e.YieldCurveRate.ValueDate:yyyy-MM-dd}");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles the failure of a yield curve rate change operation.
    /// </summary>
    /// <remarks>This method logs the failure details, resets the internal command identifier, and invokes the
    /// default cursor reset action.</remarks>
    /// <param name="e">The event containing details about the failure, including the error code and message.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask ChangeYieldCurveRateFailed(YieldCurveRateChangedFailEvent e)
    {
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rate Change failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// remove yield curve rate
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="overwrite">Indicates whether to overwrite an existing yield curve rate with the same value date.</param>
    public void RemoveYieldCurveRate(DateOnly valueDate, bool overwrite)
        => _commandModel?.Execute(async model => {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            IsArgumentNull.Check(valueDate);
            OnWaitCursor?.Invoke(); // Show wait cursor before executing the command
            WriteStatusConsole(LogSourceType.MarketData, $"Removing Yield Curve Rate: {valueDate:yyyy-MM-dd}...please wait");
            await model.RemoveYieldCurveRateAsync(valueDate, overwrite);
            OnYieldCurveRateRemoved?.Invoke(valueDate);
            OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        });

    /// <summary>
    /// Handles the completion of a yield curve rate removal operation.
    /// </summary>
    /// <remarks>This method refreshes the updated yield curve rates and resets the command state. It also
    /// invokes the <see cref="OnDefaultCursor"/> action to reset the cursor to its default state.</remarks>
    /// <param name="e">An event containing details about the removed yield curve rate, including the value date.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask RemoveYieldCurveRateCompleted(YieldCurveRateRemovedCompleteEvent e)
    {
        RefreshUpdatedYieldCurveRates($"Yield Curve Rate: {e.ValueDate:yyyy-MM-dd} Removed");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles the failure event for removing a yield curve rate.
    /// </summary>
    /// <remarks>This method logs the failure details, resets the internal command identifier, and invokes the
    /// default cursor reset action.</remarks>
    /// <param name="e">The event containing details about the failure, including the error code and message.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask RemoveYieldCurveRateFailed(YieldCurveRateRemovedFailEvent e)
    {
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rate Remove failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// import yiele curve rates
    /// </summary>
    /// <param name="importDate"></param>
    public void ImportYieldCurveRates(DateTime importDate)
        => _commandModel?.Execute(async model => {
            IsArgumentNull.Check(importDate);
            OnWaitCursor?.Invoke();
            YieldCurveRateReadModel[] yieldCurveRates = [];
            await (_queryModel?.GetExternalYieldCurveRatesAsync(e => yieldCurveRates = e))!;
            await model.ImportYieldCurveRatesAsync(importDate, yieldCurveRates);
        });

    /// <summary>
    /// Handles the completion of the yield curve rates import process.
    /// </summary>
    /// <remarks>This method is typically invoked when the yield curve rates import process has finished.  It
    /// updates the relevant state and logs the import details.</remarks>
    /// <param name="e">An event containing details about the completed import, including the imported yield curve rates and the import
    /// date.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask ImportYieldCurveRatesCompleted(YieldCurveRatesImportedCompleteEvent e)
    {
        RefreshUpdatedYieldCurveRates($"{e.YieldCurveRates.Length} Yield Curve Rates Imported on: {e.ImportDate:yyyy-MM-dd}");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles the event triggered when the import of yield curve rates fails.
    /// </summary>
    /// <remarks>This method logs the failure details, resets the command identifier, and invokes the default
    /// cursor reset action.</remarks>
    /// <param name="e">The event data containing details about the failure, including the error code and error message.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    ValueTask ImportYieldCurveRatesFailed(YieldCurveRatesImportedFailEvent e)
    {
        WriteStatusConsole(LogSourceType.MarketData, e.ErrorCode, $"Yield Curve Rates Import failed due to: {e.ErrorMessage}");
        _commandId = Guid.Empty;
        OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Loads the yield curve rate time periods and triggers the corresponding event upon completion.
    /// </summary>
    /// <remarks>This method asynchronously retrieves yield curve rate time periods and invokes the  <see
    /// cref="OnYieldCurveRateTimePeriodsLoaded"/> event with the retrieved data.  Ensure that the event is subscribed
    /// to before calling this method to handle the loaded data.</remarks>
    public void LoadYieldCurveRateTimePeriods()
         => _queryModel?.Execute(async model => {
                OnWaitCursor?.Invoke(); // Show wait cursor before executing the command
                await model.GetYieldCurveRateTimePeriodsAsync(timePeriods => OnYieldCurveRateTimePeriodsLoaded?.Invoke(timePeriods));
                OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
         });

    /// <summary>
    /// Loads yield curve rates for the specified date range and triggers an event upon completion.
    /// </summary>
    /// <remarks>This method retrieves yield curve rates asynchronously for the specified date range and
    /// invokes the  <see cref="OnYieldCurveRatesLoaded"/> event with the retrieved data. Ensure that the <see
    /// cref="_queryModel"/>  is properly initialized before calling this method.</remarks>
    /// <param name="startDate">The start date of the range for which to load yield curve rates.</param>
    /// <param name="endDate">The end date of the range for which to load yield curve rates.</param>
    public void LoadYieldCurveRates(DateOnly startDate, DateOnly endDate)
        => _queryModel?.Execute(async model => {
            OnWaitCursor?.Invoke();
            await model.GetYieldCurveRatesAsync(startDate, endDate, yieldCurveRates => OnYieldCurveRatesLoaded?.Invoke(yieldCurveRates));
            OnDefaultCursor?.Invoke(); // Reset cursor to default after operation
        });

    /// <summary>
    /// Retrieves the yield curve rate at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the yield curve rate to retrieve. Must be within the range of available rates.</param>
    /// <returns>A <see cref="YieldCurveRateReadModel"/> representing the yield curve rate at the specified index,  or <see
    /// langword="null"/> if the index is out of range.</returns>
    public YieldCurveRateReadModel? GetYieldCurveRate(int index)
        =>  (index < 0 || index >= _yieldCurveRates.Count)
            ? default
            : _yieldCurveRates[index];

    /// <summary>
    /// Sets the yield curve rates for the current instance.
    /// </summary>
    /// <param name="yieldCurveRates">An array of <see cref="YieldCurveRateReadModel"/> objects representing the yield curve rates to be set.  Cannot
    /// be <see langword="null"/>.</param>
    public void SetYieldCurveRates(YieldCurveRateReadModel[] yieldCurveRates)
    {
        if (yieldCurveRates is not null)
            _yieldCurveRates = [.. yieldCurveRates];
    }

    /// <summary>
    /// Refreshes and updates the yield curve rates by invoking the necessary actions and updating the console status.
    /// </summary>
    /// <remarks>This method triggers the <see cref="OnShowYieldCurveRates"/> event, loads the yield curve
    /// rate time periods,  and writes the specified status message to the console. Ensure that the <paramref
    /// name="consoleStatus"/> parameter  is not null or empty to provide meaningful feedback in the console.</remarks>
    /// <param name="consoleStatus">The status message to be written to the console, providing context for the operation.</param>
    void RefreshUpdatedYieldCurveRates(string consoleStatus)
    {
        OnShowYieldCurveRates?.Invoke();
        LoadYieldCurveRateTimePeriods();
        WriteStatusConsole(LogSourceType.MarketData, consoleStatus);
    }

}
