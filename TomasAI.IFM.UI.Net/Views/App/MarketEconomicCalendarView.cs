using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Views.App;

public partial class MarketEconomicCalendarView : UserControl, IFormControl
{
    MarketEconomicCalendarReadModel? _viewModel;

    public MarketEconomicCalendarView()
    {
        InitializeComponent();
    }

    public void LoadView(IAppRoot appRoot)
    {
        // create calendar view model...
        _viewModel = new MarketEconomicCalendarReadModel(appRoot)
        {
            // set updated actions...
            OnErrorMessage = e => this.ShowErrorMessage(e, "Market Economic Calendar View"),
            OnModelUpdate = e => this.Post(() => OnModelUpdate(e)),
            OnCalendarDateUpdate = (e, ec) => this.Post(() => OnCalendarDateUpdate(e, ec)),
            OnCountryCodesLoaded = () => this.Post(() => OnCountryCodesLoaded())
        };
        _viewModel.StartEventListeners(() => this.Post(() => RefreshView()));
        _viewModel.LoadCountryCodes();
    }

    public void RefreshView()
    {
        var todaysDate = DateTime.Now.Date;
        var calendarPeriod = _viewModel!.GetEconomicCalendarViewType(tabCalendarPeriod.SelectedTab!.Text);
        _viewModel.UpdateModel(todaysDate, calendarPeriod!);
    }

    internal void OnModelUpdate(EconomicCalendarReadModel[] economicCalendar)
    {
        lstEconomicCalendar.Items.Clear();
        if (economicCalendar != null && economicCalendar.Length > 0)
        {
            foreach (var e in economicCalendar)
                lstEconomicCalendar.Items.Add(new ListViewItem([
                    $"{e.EventDate:t}",
                    $"{e.CountryCode}",
                    $"{e.EventName}"
                ]));
            lstEconomicCalendar.Items[0].Selected = true;
        }
    }

    internal void OnCalendarDateUpdate(string calendarDate, EconomicCalendarReadModel ec)
    {
        txtCalendarDate.Text = calendarDate;
        txtActual.Text = !string.IsNullOrWhiteSpace(ec?.Actual) ? ec.Actual : string.Empty;
        txtForecast.Text = !string.IsNullOrWhiteSpace(ec?.Forecast) ? ec.Forecast : string.Empty;
        txtPrior.Text = !string.IsNullOrWhiteSpace(ec?.Prior) ? ec.Prior : string.Empty;
    }

    internal void OnCountryCodesLoaded()
    {
        ddlCountryCodes.Items.Clear();
        if (_viewModel!.CountryCodes.Count > 0)
        {
            foreach (var countryCode in _viewModel.CountryCodes)
                ddlCountryCodes.Items.Add(countryCode);
            var selectedIndex = ddlCountryCodes.Items.IndexOf("US");
            ddlCountryCodes.SelectedIndex = selectedIndex == -1 ? 0 : selectedIndex;
        }
    }

    public void Open() { }
    void IFormControl.Resize(Control parentControl) { }

    public void Close() => _viewModel!.StopEventListeners();

    private void tabCalendarPeriod_SelectedIndexChanged(object sender, EventArgs e)
    {
        RefreshView();
    }

    private void lstEconomicCalendar_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstEconomicCalendar.SelectedIndices.Count > 0)
        {
            var listIndex = lstEconomicCalendar.SelectedIndices[0];
            var economicCalendar = _viewModel!.GetEconomicCalendar(listIndex);
            var calendarDate = _viewModel.GetCalendarDate(listIndex);
            if (economicCalendar is not null && calendarDate.HasValue)
                OnCalendarDateUpdate($"{calendarDate.Value.DayOfWeek}, {calendarDate.Value:MMMM} {calendarDate.Value:dd}, {calendarDate.Value:yyyy}", economicCalendar);
        }
    }

    private void ddlCountryCodes_SelectedIndexChanged(object sender, EventArgs e)
    {
        _viewModel!.SetSelectedCountryCode(ddlCountryCodes.SelectedIndex);
        // refresh calendar view...
        RefreshView();
    }
}
