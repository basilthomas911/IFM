using System.ComponentModel;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>
/// Transitional WinForms adapter for observable economic-calendar state.
/// </summary>
public partial class MarketEconomicCalendarView : UserControl, IAsyncFormControl
{
    MarketEconomicCalendarViewModel? _viewModel;
    bool _rendering;
    long _lastErrorSequence;

    public MarketEconomicCalendarView()
    {
        InitializeComponent();
    }

    /// <summary>Creates, starts, and loads the lifecycle-owned calendar ViewModel.</summary>
    public async Task LoadViewAsync(IAppRoot appRoot)
    {
        _viewModel = new MarketEconomicCalendarViewModel(appRoot);
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        _viewModel.SelectCalendarPeriod(
            tabCalendarPeriod.SelectedTab?.Text ?? "Today",
            DateTime.Now);

        try
        {
            await _viewModel.InitializeAsync(CancellationToken.None);
            await _viewModel.LoadCountryCodesOperation.ExecuteAsync();
            await _viewModel.RefreshOperation.ExecuteAsync();
            RenderObservableState();
        }
        catch (Exception exception)
        {
            ShowOperationFailure(exception, "Market Economic Calendar View");
        }
    }

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            if (_viewModel is null)
                return;

            switch (eventArgs.PropertyName)
            {
                case nameof(MarketEconomicCalendarViewModel.CountryCodes):
                case nameof(MarketEconomicCalendarViewModel.SelectedCountryCode):
                    RenderCountryCodes();
                    break;
                case nameof(MarketEconomicCalendarViewModel.EconomicCalendars):
                    RenderCalendars();
                    break;
                case nameof(MarketEconomicCalendarViewModel.CalendarDate):
                case nameof(MarketEconomicCalendarViewModel.SelectedEconomicCalendar):
                    RenderDetails();
                    break;
                case nameof(MarketEconomicCalendarViewModel.LastError):
                    RenderLatestError();
                    break;
            }
        });

    void RenderObservableState()
    {
        RenderCountryCodes();
        RenderCalendars();
        RenderDetails();
        RenderLatestError();
    }

    void RenderCountryCodes()
    {
        if (_viewModel is null)
            return;

        _rendering = true;
        try
        {
            ddlCountryCodes.Items.Clear();
            ddlCountryCodes.Items.AddRange(_viewModel.CountryCodes.Cast<object>().ToArray());
            ddlCountryCodes.SelectedIndex = _viewModel.CountryCodes
                .Select((code, index) => (code, index))
                .Where(value => value.code == _viewModel.SelectedCountryCode)
                .Select(value => value.index)
                .DefaultIfEmpty(-1)
                .First();
        }
        finally
        {
            _rendering = false;
        }
    }

    void RenderCalendars()
    {
        if (_viewModel is null)
            return;

        _rendering = true;
        try
        {
            lstEconomicCalendar.Items.Clear();
            lstEconomicCalendar.Items.AddRange(_viewModel.EconomicCalendars
                .Select(calendar => new ListViewItem([
                    $"{calendar.EventDate:t}",
                    calendar.CountryCode,
                    calendar.EventName]))
                .ToArray());
            if (lstEconomicCalendar.Items.Count > 0)
                lstEconomicCalendar.Items[0].Selected = true;
        }
        finally
        {
            _rendering = false;
        }
    }

    void RenderDetails()
    {
        if (_viewModel is null)
            return;

        var selected = _viewModel.SelectedEconomicCalendar;
        txtCalendarDate.Text = _viewModel.CalendarDate;
        txtActual.Text = selected?.Actual ?? string.Empty;
        txtForecast.Text = selected?.Forecast ?? string.Empty;
        txtPrior.Text = selected?.Prior ?? string.Empty;
    }

    void RenderLatestError()
    {
        if (_viewModel?.LastError is not { } error || error.Sequence <= _lastErrorSequence)
            return;

        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    async Task RefreshViewAsync()
    {
        if (_viewModel is null)
            return;

        _viewModel.SelectCalendarPeriod(
            tabCalendarPeriod.SelectedTab?.Text ?? "Today",
            DateTime.Now);
        try
        {
            await _viewModel.RefreshOperation.ExecuteAsync();
        }
        catch (Exception exception)
        {
            ShowOperationFailure(exception, "Economic Calendar Error");
        }
    }

    void ShowOperationFailure(Exception exception, string caption)
    {
        if (_viewModel?.LastError?.Message == exception.Message)
        {
            RenderLatestError();
            return;
        }

        this.ShowErrorMessage(exception.Message, caption);
    }

    public void Open() { }

    void IFormControl.Resize(Control parentControl) { }

    public void Close() => _ = ((IAsyncFormControl)this).CloseAsync();

    async ValueTask IAsyncFormControl.CloseAsync()
    {
        if (_viewModel is null)
            return;

        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        await _viewModel.DisposeAsync();
        _viewModel = null;
    }

    async void tabCalendarPeriod_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (!_rendering)
            await RefreshViewAsync();
    }

    void lstEconomicCalendar_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering || _viewModel is null || lstEconomicCalendar.SelectedIndices.Count == 0)
            return;

        _viewModel.SelectEconomicCalendar(lstEconomicCalendar.SelectedIndices[0]);
    }

    async void ddlCountryCodes_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rendering || _viewModel is null
            || !_viewModel.SelectCountryCode(ddlCountryCodes.SelectedIndex))
        {
            return;
        }

        await RefreshViewAsync();
    }
}
