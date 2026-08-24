using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Models.Reference;

namespace TomasAI.IFM.UI.Net.Views.Reference
{
    /// <summary>
    /// Adapts economic-calendar maintenance and correlated imports to WinForms controls.
    /// </summary>
    public partial class EconomicCalendarEditorView : UserControl, IControlCommand, IAsyncFormControl
    {
        readonly EconomicCalendarEditorViewModel _viewModel;
        EditMode _editMode;
#pragma warning disable CS0649 // Field is never assigned to
        int _lastCalendarEventIndex;
#pragma warning restore CS0649
        bool _canChangeRemove;
        bool _isBindingCountry;

        /// <summary>
        /// economic calendar editor constructor
        /// </summary>
        /// <param name="viewModel"></param>
        public EconomicCalendarEditorView(EconomicCalendarEditorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            UpdateCountryCodeAccessibility();
        }

        public bool CanChangeRemove => _canChangeRemove;

        public bool CanImport => true;

        /// <summary>
        /// load reference data
        /// </summary>
        /// <param name="appRoot"></param>
        /// <param name="dataLoaded"></param>
        void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
        {
            _editMode = EditMode.View;
            _viewModel.StartWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.WaitCursor);
            _viewModel.StopWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.Default);

            _viewModel.OnError = (_, errorMsg) => this.Post(() =>
                MessageBox.Show(
                    text: errorMsg,
                    caption: "Economic Calendar Editor Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error));

            _viewModel.OnEconomicCalendarsLoaded = () => this.Post(() =>
            {
                _canChangeRemove = false;
                var selectedIndex = lstCalendarEvents.SelectedIndex;
                lstCalendarEvents.Items.Clear();
                if (_viewModel.EconomicCalendars?.Count > 0)
                {
                    foreach (var ec in _viewModel.EconomicCalendars!)
                        lstCalendarEvents.Items.Add($"{ec.CountryCode}:{ec.EventName}");
                    lstCalendarEvents.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
                    dtmEventDate.Enabled = true;
                    _canChangeRemove = true;
                }
                dataLoaded?.Invoke(_canChangeRemove);
                dtmEventDate.Enabled = false;
                ShowSelectedEconomicCalendar(lstCalendarEvents.SelectedIndex < 0 ? 0 : lstCalendarEvents.SelectedIndex);
                dtmEventDate.Enabled = true;
            });

            _viewModel.OnCountryCodesLoaded = () => this.Post(() =>
            {
                _isBindingCountry = true;
                ddlCountryCodes.Items.Clear();
                if (_viewModel.CountryCodes?.Count == 0)
                {
                    _isBindingCountry = false;
                    return;
                }
                foreach (var e in _viewModel.CountryCodes!)
                    ddlCountryCodes.Items.Add(e.CountryCode);
                var selectedIndex = ddlCountryCodes.Items.IndexOf("US");
                ddlCountryCodes.SelectedIndex = selectedIndex == -1 ? 0 : selectedIndex;
                _isBindingCountry = false;
                ddlCountryCodes.Enabled = true;
                UpdateCountryCodeAccessibility();
                var countryCode = _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? string.Empty;
                _ = LoadCalendarsAsync(DateOnly.FromDateTime(dtmEventDate.Value), countryCode);
            });

            _viewModel.OnWaitCursor = () => this.Post(() => Cursor = Cursors.WaitCursor);
            _viewModel.OnDefaultCursor = () => this.Post(() => Cursor = Cursors.Default);

            _ = LoadEditorAsync();
        }

        /// <summary>
        /// Stops the economic-calendar listener and any active import.
        /// </summary>
        void IControlCommand.Unload()
        {
            _ = ((IAsyncFormControl)this).CloseAsync();
        }

        /// <summary>
        /// add economic calendar
        /// </summary>
        public void Add(Action<bool> addAction)
        {
            if (_viewModel.ImportOperation.IsRunning)
                return;
            switch (_editMode)
            {
                case EditMode.View:
                    dtmEventDate.Enabled = false;
                    _editMode = EditMode.Add;
                    ddlCountryCodes.Enabled = true;
                    ddlCountryCodes.SelectedIndex = 0;
                    txtEventName.Text = String.Empty;
                    txtActual.Text = String.Empty;
                    txtForecast.Text = String.Empty;
                    txtPrior.Text = String.Empty;
                    addAction(false);
                    lstCalendarEvents.Enabled = false;
                    txtEventName.ReadOnly = false;
                    SetReadOnlyControls(false);
                    break;
                case EditMode.Add:
                    var economicCalendar = new EconomicCalendarUiModel
                    (
                        EventDate: EasternTime.ToUtc(dtmEventDate.Value),
                        CountryCode: _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? String.Empty,
                        EventName: txtEventName.Text,
                        Actual: txtActual.Text,
                        Forecast: txtForecast.Text,
                        Prior: txtPrior.Text,
                        CreatedOn: DateTime.UtcNow,
                        CreatedBy: String.Empty
                    );
                    ObserveMutation(_viewModel.AddEconomicCalendar(economicCalendar, () => this.Post(() =>
                    {
                        _editMode = EditMode.View;
                        lstCalendarEvents.Enabled = true;
                        txtEventName.ReadOnly = true;
                        ddlCountryCodes.Enabled = true;
                        SetReadOnlyControls(true);
                        addAction(true);
                    })), "Economic Calendar Add Failed");
                    break;
            }
        }

        /// <summary>
        /// change economic calendar
        /// </summary>
        /// <param name="changeAction"></param>
        public void Change(Action<bool> changeAction)
        {
            if (_viewModel.ImportOperation.IsRunning)
                return;
            var selectedCalendar = _viewModel.GetEconomicCalendar(lstCalendarEvents.SelectedIndex);
            if (selectedCalendar != null)
            {
                switch (_editMode)
                {
                    case EditMode.View:
                        dtmEventDate.Value = EasternTime.FromUtc(selectedCalendar.EventDate);
                        dtmEventDate.Enabled = false;
                        ddlCountryCodes.Enabled = false;
                        _editMode = EditMode.Change;
                        changeAction(false);
                        lstCalendarEvents.Enabled = false;
                        SetReadOnlyControls(false);
                        break;
                    case EditMode.Change:
                        var economicCalendar = new EconomicCalendarUiModel
                        (
                           EventDate: selectedCalendar.EventDate,
                           CountryCode: _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? String.Empty,
                           EventName: txtEventName.Text,
                           Actual: txtActual.Text,
                           Forecast: txtForecast.Text,
                           Prior: txtPrior.Text,
                           CreatedOn: DateTime.UtcNow,
                           CreatedBy: String.Empty
                        );
                        ObserveMutation(_viewModel.ChangeEconomicCalendar(
                            selectedCalendar.EventDate,
                            selectedCalendar.CountryCode,
                            selectedCalendar.EventName,
                            economicCalendar,
                            true,
                            () => this.Post(() =>
                        {
                            _editMode = EditMode.View;
                            dtmEventDate.Enabled = true;
                            ddlCountryCodes.Enabled = true;
                            lstCalendarEvents.Enabled = true;
                            SetReadOnlyControls(true);
                            changeAction(true);
                        })), "Economic Calendar Change Failed");
                        break;
                }
            }

        }

        /// <summary>
        /// remove selected economic calendar
        /// </summary>
        public void Remove()
        {
            if (_viewModel.ImportOperation.IsRunning)
                return;
            var selectedCalendar = _viewModel.GetEconomicCalendar(lstCalendarEvents.SelectedIndex);
            if (selectedCalendar != null)
                if (MessageBox.Show($"Are you sure you want to remove Economic Calendar {selectedCalendar.CountryCode}:{selectedCalendar.EventName} ?", "Remove Economic Calendar", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    ObserveMutation(
                        _viewModel.RemoveEconomicCalendar(
                            selectedCalendar.EventDate,
                            selectedCalendar.CountryCode,
                            selectedCalendar.EventName,
                            true),
                        "Economic Calendar Remove Failed");
        }

        public void Import()
        {
            if (_viewModel.ImportOperation.IsRunning)
                return;
            var countryCode = _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(countryCode))
            {
                this.ShowErrorMessage(
                    "Select an economic-calendar country before importing.",
                    "Economic Calendar Import");
                return;
            }
            _viewModel.PrepareImport(dtmEventDate.Value.Date, countryCode);
            _ = ImportPreparedCalendarsAsync();
        }

        void ObserveMutation(Task operation, string caption)
            => _ = ObserveMutationAsync(operation, caption);

        async Task ObserveMutationAsync(Task operation, string caption)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException)
            {
                // Closing the editor cancels local observation without manufacturing a domain failure.
            }
            catch (Exception exception)
            {
                this.ShowErrorMessage(exception.Message, caption);
            }
        }

        async Task LoadEditorAsync()
        {
            try
            {
                await _viewModel.LoadCountryCodes();
            }
            catch (Exception exception)
            {
                this.ShowErrorMessage(exception.Message, "Economic Calendar Editor Error");
            }
        }

        async Task LoadCalendarsAsync(DateOnly eventDate, string countryCode)
        {
            try
            {
                await _viewModel.LoadEconomicCalendars(eventDate, countryCode);
            }
            catch (Exception exception)
            {
                this.ShowErrorMessage(exception.Message, "Economic Calendar Editor Error");
            }
        }

        async Task ImportPreparedCalendarsAsync()
        {
            Cursor = Cursors.WaitCursor;
            Enabled = false;
            try
            {
                await _viewModel.ImportOperation.ExecuteAsync();
                MessageBox.Show(
                    text: _viewModel.LastStatusMessage,
                    caption: "Economic Calendar Import",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                // Closing the editor cancels local observation; it does not manufacture a domain failure.
            }
            catch (Exception exception)
            {
                this.ShowErrorMessage(exception.Message, "Economic Calendar Import Failed");
            }
            finally
            {
                Cursor = Cursors.Default;
                Enabled = true;
            }
        }

        /// <summary>
        /// close/cancel economic calendar editor
        /// </summary>
        /// <param name="closeAction"></param>
        /// <returns></returns>
        public bool Close(Action<bool> closeAction)
        {
            switch (_editMode)
            {
                case EditMode.Add:
                case EditMode.Change:
                    ShowSelectedEconomicCalendar(_lastCalendarEventIndex);
                    _editMode = EditMode.View;
                    closeAction?.Invoke(lstCalendarEvents.Items.Count > 0);
                    lstCalendarEvents.Enabled = true;
                    txtEventName.ReadOnly = true;
                    ddlCountryCodes.Enabled = true;
                    SetReadOnlyControls(true);
                    return false;
            }
            return true;
        }

        /// <summary>
        /// show selected futures contract details
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void lstCalendarEvents_SelectedIndexChanged(object sender, EventArgs e)
        {
            dtmEventDate.Enabled = false;
            ShowSelectedEconomicCalendar(lstCalendarEvents.SelectedIndex);
            dtmEventDate.Enabled = true;
        }

        /// <summary>
        /// show futures contract details
        /// </summary>
        /// <param name="selectedIndex"></param>
        void ShowSelectedEconomicCalendar(int selectedIndex)
        {
            var ec = _viewModel.GetEconomicCalendar(selectedIndex);
            if (ec is not null)
                dtmEventDate.Value = EasternTime.FromUtc(ec.EventDate);
            if (ec is not null)
            {
                _isBindingCountry = true;
                ddlCountryCodes.SelectedIndex = _viewModel.GetCountryCodeIndex(ec.CountryCode);
                _isBindingCountry = false;
            }
            txtEventName.Text = ec?.EventName ?? String.Empty;
            txtEventName.ReadOnly = true;
            txtActual.Text = ec?.Actual ?? String.Empty;
            txtForecast.Text = ec?.Forecast ?? String.Empty;
            txtPrior.Text = ec?.Prior ?? String.Empty;
            SetReadOnlyControls(true);
        }

        enum EditMode
        {
            View,
            Add,
            Change
        }

        void SetReadOnlyControls(bool readOnly)
        {
            txtActual.ReadOnly = readOnly;
            txtForecast.ReadOnly = readOnly;
            txtPrior.ReadOnly = readOnly;
            txtActual.BorderStyle = readOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
            txtForecast.BorderStyle = readOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
            txtPrior.BorderStyle = readOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
            txtEventName.BorderStyle = txtEventName.ReadOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
        }

        void ddlCountryCodes_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCountryCodeAccessibility();
            if (_isBindingCountry || _editMode != EditMode.View || ddlCountryCodes.SelectedIndex < 0)
                return;
            var countryCode = _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(countryCode))
                _ = LoadCalendarsAsync(DateOnly.FromDateTime(dtmEventDate.Value), countryCode);
        }

        void UpdateCountryCodeAccessibility()
        {
            var selectedCountry = ddlCountryCodes.SelectedItem?.ToString();
            var label = string.IsNullOrWhiteSpace(selectedCountry)
                ? "Economic calendar country"
                : $"Economic calendar country: {selectedCountry}";
            ddlCountryCodes.AccessibleDescription = string.Join(", ", ddlCountryCodes.Items.Cast<object>());
            ddlCountryCodes.AccessibleName = $"{label}; catalog: {ddlCountryCodes.AccessibleDescription}";
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        void IFormControl.Resize(Control parentControl)
        {
            throw new NotImplementedException();
        }

        public void Close() => _ = ((IAsyncFormControl)this).CloseAsync();

        async ValueTask IAsyncFormControl.CloseAsync()
            => await _viewModel.StopAsync(CancellationToken.None);

        private void dtmEventDate_ValueChanged(object sender, EventArgs e)
        {
            if (dtmEventDate.Enabled)
            {
                var countryCode = _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? string.Empty;
                _ = LoadCalendarsAsync(DateOnly.FromDateTime(dtmEventDate.Value), countryCode);
            }
        }

        private void txtActual_TextChanged(object sender, EventArgs e)
        {

        }
    }

}
