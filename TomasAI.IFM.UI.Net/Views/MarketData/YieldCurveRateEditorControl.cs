using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.ViewModels.MarketData;

namespace TomasAI.IFM.Views.MarketData;

/// <summary>
/// Represents a user control for managing and editing yield curve rates.
/// </summary>
/// <remarks>This control provides functionality to load, add, change, remove, and import yield curve rates. It
/// interacts with a <see cref="YieldCurveRateEditorViewModel"/> to handle data operations and updates the UI
/// accordingly. The control also supports commands defined by the <see cref="IControlCommand"/> and <see
/// cref="IFormControl"/> interfaces.</remarks>
public partial class YieldCurveRateEditorControl 
    : UserControl, IControlCommand, IFormControl
{
    YieldCurveRateEditorViewModel? _viewModel;

    /// <summary>
    /// Gets a value indicating whether the "Remove" operation can be changed.
    /// </summary>
    public bool CanChangeRemove 
        => _viewModel?.CanChangeRemove ?? false;

    /// <summary>
    /// Gets a value indicating whether the current context supports importing operations.
    /// </summary>
    public bool CanImport 
        => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="YieldCurveRateEditorControl"/> class with the specified view model.
    /// </summary>
    /// <param name="viewModel">The view model that provides data and behavior for the yield curve rate editor control. Cannot be null.</param>
    public YieldCurveRateEditorControl(YieldCurveRateEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    /// <summary>
    /// Loads the necessary data and initializes event handlers for managing yield curve rates.
    /// </summary>
    /// <remarks>This method sets up event handlers for various operations related to yield curve rates, such
    /// as loading, adding, changing, removing, and importing rates. It also handles UI updates, error notifications,
    /// and cursor state changes during these operations. The provided <paramref name="dataLoaded"/> callback is
    /// triggered once the yield curve rates are loaded, with a value of <see langword="true"/> if data is available, or
    /// <see langword="false"/> otherwise.</remarks>
    /// <param name="appRoot">The application root object used for accessing shared application resources.</param>
    /// <param name="dataLoaded">A callback action invoked with a boolean value indicating whether the data was successfully loaded.</param>
    void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
    {
        bool showError = false; 
        _viewModel?.OnDataLoaded = dataLoaded;
        
        _viewModel?.OnError = (_, errorMsg) => this.Post(() =>
        {
            if (!showError)
            {
                showError = true;
                MessageBox.Show(
                    text: errorMsg,
                    caption: "Yield Curve Rates Editor Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                showError = false;
            }
        });

        _viewModel?.OnYieldCurveRateTimePeriodsLoaded = timePeriods =>
            this.Post(() => {
                   if (timePeriods is not null && timePeriods.Length > 0)
                   {
                       ddlTimePeriod.DataSource = timePeriods;
                       ddlTimePeriod.SelectedIndex = 0;
                   }
               });

        _viewModel?.OnYieldCurveRateAdded = (ycr) => this.Post(() => {
            ShowYieldCurveRates();
            _viewModel.OnAddAction?.Invoke(true);
            _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"Yield Curve Rate for: {ycr.ValueDate:dd-MM-yyyy} added");
        });

        _viewModel?.OnYieldCurveRateChanged = (ycr) => this.Post(() =>
        {
            ShowYieldCurveRates();
            _viewModel.OnChangeAction?.Invoke(true);
            _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"Yield Curve Rate for: {ycr.ValueDate:dd-MM-yyyy} changed");
        });

        _viewModel?.OnYieldCurveRateRemoved = (valueDate) => this.Post(() =>
        {
            ShowYieldCurveRates();
            _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"Yield Curve Rate for: {valueDate:dd-MM-yyyy} was Removed");
        });

        _viewModel?.OnYieldCurveRatesImported = (numImported) => this.Post(() => {
            ShowYieldCurveRates();
            _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"{numImported} Yield Curve Rates Imported");
        });

        _viewModel?.OnYieldCurveRatesLoaded = (yieldCurveRates) => this.Post(() => {
            _viewModel.CanChangeRemove = false;
            if (yieldCurveRates?.Length > 0)
            {
                _viewModel.CanChangeRemove = true;
                yieldCurveRatesBindingSource.DataSource = yieldCurveRates;
                gridYieldCurveRates.DataSource = yieldCurveRatesBindingSource;
                yieldCurveRatesBindingSource.ResetBindings(false);
                gridYieldCurveRates.Update();
            }
            _viewModel.OnDataLoaded?.Invoke(yieldCurveRates?.Length > 0);
            if (yieldCurveRates?.Length > 0)
                _viewModel.SetYieldCurveRates(yieldCurveRates);
        });

        _viewModel?.OnShowYieldCurveRates = () => this.Post(() => {
            ShowYieldCurveRates();
        });

        _viewModel?.OnWaitCursor = () => this.Post(() =>
        {
            Cursor = Cursors.WaitCursor;
        });

        _viewModel?.OnDefaultCursor = () => this.Post(() =>
        {
            Cursor = Cursors.Default;
        });

        _viewModel?.LoadYieldCurveRateTimePeriods();
    }

    /// <summary>
    /// Releases resources or performs cleanup operations associated with the control.
    /// </summary>
    /// <remarks>This method is typically called to unload the control and free any resources it holds. 
    /// Ensure that any dependent operations or references are completed before invoking this method.</remarks>
    void IControlCommand.Unload()
    {
    }

    /// <summary>
    /// Displays a dialog for adding a new yield curve rate and processes the result based on user input.
    /// </summary>
    /// <remarks>This method opens a dialog to allow the user to input a new yield curve rate.  If the user
    /// confirms the operation, the yield curve rate is added to the view model.  If the user cancels, the provided
    /// <paramref name="addAction"/> is invoked with <see langword="true"/>.</remarks>
    /// <param name="addAction">A callback action that is invoked after the dialog is closed.  If the user cancels the dialog, the action is
    /// invoked with <see langword="true"/>.</param>
    public void Add(Action<bool> addAction)
    {
        var dlg = new YieldCurveRateEditForm(_viewModel!.AppRoot);
        switch (dlg.ShowDialog())
        {
            case DialogResult.OK:
                _viewModel.OnAddAction = addAction;
                _viewModel.AddYieldCurveRate(dlg.YieldCurveRate, false);
                break;
            case DialogResult.Cancel:
                addAction(true);
                break;
        }
    }

    /// <summary>
    /// Initiates a change operation for a selected yield curve rate and executes the specified action based on the
    /// result.
    /// </summary>
    /// <remarks>This method retrieves the currently selected yield curve rate from the grid and opens a
    /// dialog for editing it.  If the dialog result is <see cref="DialogResult.OK"/>, the yield curve rate is updated,
    /// and the provided action is not invoked.  If the dialog result is <see cref="DialogResult.Cancel"/>, the provided
    /// action is invoked with a <see langword="true"/> value.</remarks>
    /// <param name="changeAction">An <see cref="Action{T}"/> delegate that is invoked with a <see langword="true"/> value if the operation is
    /// canceled,  or remains uninvoked if the operation completes successfully.</param>
    public void Change(Action<bool> changeAction)
    {
        var yieldCurveRate = _viewModel!.GetYieldCurveRate(gridYieldCurveRates.SelectedRows[0].Index);
        if (yieldCurveRate is null)
        {
            _viewModel!.WriteStatusConsole(LogSourceType.MarketData, "No Yield Curve Rate selected for change");
            return;
        }
        var dlg = new YieldCurveRateEditForm(_viewModel!.AppRoot);
        dlg.SetYieldCurveRate(yieldCurveRate);
        switch (dlg.ShowDialog())
        {
            case DialogResult.OK:
                _viewModel.OnChangeAction = changeAction;
                _viewModel.ChangeYieldCurveRate(dlg.YieldCurveRate, true);
                break;
            case DialogResult.Cancel:
                changeAction(true);
                break;
        }
    }

    /// <summary>
    /// Closes the current resource and invokes the specified callback with the result of the operation.
    /// </summary>
    /// <param name="changeAction">A callback action that is invoked with a <see langword="true"/> value to indicate the success of the close
    /// operation.</param>
    /// <returns><see langword="true"/> if the resource was successfully closed; otherwise, <see langword="false"/>.</returns>
    public bool Close(Action<bool> changeAction) 
        => true;

    /// <summary>
    /// Removes the selected yield curve rate after user confirmation.
    /// </summary>
    /// <remarks>Displays a confirmation dialog to the user before removing the yield curve rate associated
    /// with the selected row. If the user confirms, the yield curve rate is removed from the underlying data
    /// source.</remarks>
    public void Remove()
    {
        var yieldCurveRate = _viewModel!.GetYieldCurveRate(gridYieldCurveRates.SelectedRows[0].Index);
        if (yieldCurveRate is null)
        {
            _viewModel!.WriteStatusConsole(LogSourceType.MarketData, "No Yield Curve Rate selected for removal");
            return;
        }
        if (MessageBox.Show($"Are you sure you want to remove the Yield Curve Rates for: {yieldCurveRate.ValueDate:yyyy-MMM-dd} ?"
                , "Remove Yield Curve Rate", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _viewModel.RemoveYieldCurveRate(yieldCurveRate.ValueDate, true);
        }
    }

    /// <summary>
    /// Initiates the import process for yield curve rates.
    /// </summary>
    /// <remarks>This method logs the import operation to the status console and triggers the import of yield
    /// curve rates for the current date. Ensure that the associated view model is properly initialized before calling
    /// this method.</remarks>
    public void Import() 
    {
        _viewModel!.WriteStatusConsole(LogSourceType.MarketData, "Importing Yield Curve Rates...");
        _viewModel.ImportYieldCurveRates(DateTime.Now.Date);
    }

    /// <summary>
    /// Displays yield curve rates for a specified time period.
    /// </summary>
    /// <remarks>The method determines the date range based on the selected time period and loads the
    /// corresponding yield curve rates  using the provided date range. Supported time periods include "Current Month"
    /// and specific years.</remarks>
    void ShowYieldCurveRates()
    {
        (DateOnly startDate, DateOnly endDate) dateRange;
        var timePeriod = $"{ddlTimePeriod.SelectedItem}";
        switch (timePeriod)
        {
            case "Current Month":
                var currentDate = DateTime.Now;
                dateRange.startDate = new DateOnly(currentDate.Year, currentDate.Month, 1);
                dateRange.endDate = dateRange.startDate.AddMonths(1).AddDays(-1);
                break;
            default:
                dateRange.startDate = new DateOnly(Convert.ToInt32(timePeriod), 1, 1);
                dateRange.endDate = dateRange.startDate.AddYears(1).AddDays(-1);
                break;
        }
        _viewModel!.LoadYieldCurveRates(dateRange.startDate, dateRange.endDate);
    }

    /// <summary>
    /// Opens the resource or connection associated with this instance.
    /// </summary>
    /// <remarks>This method is intended to initialize and make the resource or connection ready for use. 
    /// Ensure that any required preconditions, such as configuration or dependencies, are met before calling this
    /// method.</remarks>
    /// <exception cref="NotImplementedException">This method is not yet implemented.</exception>
    public void Open()
        => throw new NotImplementedException();

    /// <summary>
    /// Closes the current resource and releases any associated resources.
    /// </summary>
    /// <remarks>Once the resource is closed, it cannot be reopened. Ensure that all necessary operations  are
    /// completed before calling this method.</remarks>
    /// <exception cref="NotImplementedException">This method is not yet implemented.</exception>
    public void Close()
        => throw new NotImplementedException();

    void ddlTimePeriod_SelectedIndexChanged(object sender, EventArgs e) 
        => ShowYieldCurveRates();

    void IFormControl.Resize(Control parentControl)
        => throw new NotImplementedException();



}
