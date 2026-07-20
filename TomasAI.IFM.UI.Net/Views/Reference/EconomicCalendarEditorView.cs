using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.ViewModels.Reference;

namespace TomasAI.IFM.Views.Reference
{
    /// <summary>
    /// futures contract editor
    /// </summary>
    public partial class EconomicCalendarEditorView : UserControl, IControlCommand, IFormControl
    {
        EconomicCalendarEditorViewModel _viewModel;
        EditMode _editMode;
#pragma warning disable CS0649 // Field is never assigned to
        int _lastCalendarEventIndex;
#pragma warning restore CS0649
        bool _canChangeRemove;

        /// <summary>
        /// economic calendar editor constructor
        /// </summary>
        /// <param name="viewModel"></param>
        public EconomicCalendarEditorView(EconomicCalendarEditorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
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
                        lstCalendarEvents.Items.Add($"{ec.Id}");
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
                ddlCountryCodes.Items.Clear();
                if (_viewModel.CountryCodes?.Count == 0)
                    return;
                foreach (var e in _viewModel.CountryCodes!)
                    ddlCountryCodes.Items.Add(e.CountryCode);
                var selectedIndex = ddlCountryCodes.Items.IndexOf("US");
                ddlCountryCodes.SelectedIndex = selectedIndex == -1 ? 0 : selectedIndex;
                _viewModel.LoadEconomicCalendars(DateOnly.FromDateTime(dtmEventDate.Value), "US");
            });

            _viewModel.OnWaitCursor = () => this.Post(() => Cursor = Cursors.WaitCursor);
            _viewModel.OnDefaultCursor = () => this.Post(() => Cursor = Cursors.Default);

            _viewModel.LoadCountryCodes();
        }

        /// <summary>
        /// unload futures contract editor
        /// </summary>
        void IControlCommand.Unload()
        {
        }

        /// <summary>
        /// add economic calendar
        /// </summary>
        public void Add(Action<bool> addAction)
        {
            switch (_editMode)
            {
                case EditMode.View:
                    dtmEventDate.Enabled = false;
                    ddlCountryCodes.Enabled = true;
                    ddlCountryCodes.SelectedIndex = 0;
                    txtEventName.Text = String.Empty;
                    txtActual.Text = String.Empty;
                    txtForecast.Text = String.Empty;
                    txtPrior.Text = String.Empty;
                    _editMode = EditMode.Add;
                    addAction(false);
                    lstCalendarEvents.Enabled = false;
                    txtEventName.ReadOnly = false;
                    SetReadOnlyControls(false);
                    break;
                case EditMode.Add:
                    var economicCalendar = new EconomicCalendarReadModel
                    (
                        eventDate: dtmEventDate.Value,
                        countryCode: _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? String.Empty,
                        eventName: txtEventName.Text,
                        actual: txtActual.Text,
                        forecast: txtForecast.Text,
                        prior: txtPrior.Text,
                        createdOn: DateTime.Now,
                        createdBy: String.Empty
                    );
                    _viewModel.AddEconomicCalendar(economicCalendar, () => this.Post(() =>
                    {
                        _editMode = EditMode.View;
                        lstCalendarEvents.Enabled = true;
                        txtEventName.ReadOnly = true;
                        ddlCountryCodes.Enabled = false;
                        SetReadOnlyControls(true);
                        addAction(true);
                    }));
                    break;
            }
        }

        /// <summary>
        /// change economic calendar
        /// </summary>
        /// <param name="changeAction"></param>
        public void Change(Action<bool> changeAction)
        {
            var economicCalendarId = _viewModel.GetEconomicCalendar(lstCalendarEvents.SelectedIndex)?.Id;
            if (economicCalendarId != null)
            {
                switch (_editMode)
                {
                    case EditMode.View:
                        dtmEventDate.Value = economicCalendarId.EventDate;
                        dtmEventDate.Enabled = false;
                        _editMode = EditMode.Change;
                        changeAction(false);
                        lstCalendarEvents.Enabled = false;
                        SetReadOnlyControls(false);
                        break;
                    case EditMode.Change:
                        var economicCalendar = new EconomicCalendarReadModel
                        (
                           eventDate: economicCalendarId.EventDate,
                           countryCode: _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? String.Empty,
                           eventName: txtEventName.Text,
                           actual: txtActual.Text,
                           forecast: txtForecast.Text,
                           prior: txtPrior.Text,
                           createdOn: DateTime.Now,
                           createdBy: String.Empty
                        );
                        _viewModel.ChangeEconomicCalendar(economicCalendarId, economicCalendar, true, () => this.Post(() =>
                        {
                            _editMode = EditMode.View;
                            dtmEventDate.Enabled = true;
                            lstCalendarEvents.Enabled = true;
                            SetReadOnlyControls(true);
                            changeAction(true);
                        }));
                        break;
                }
            }

        }

        /// <summary>
        /// remove selected economic calendar
        /// </summary>
        public void Remove()
        {
            var economicCalendarId = _viewModel.GetEconomicCalendar(lstCalendarEvents.SelectedIndex)?.Id;
            if (economicCalendarId != null)
                if (MessageBox.Show($"Are you sure you want to remove Economic Calendar {economicCalendarId} ?", "Remove Economic Calendar", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _viewModel.RemoveEconomicCalendar(economicCalendarId, true);
        }

        public void Import()
        {
            var countryCode = _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? string.Empty;
            _viewModel.ImportEconomicCalendars(DateTime.Now, countryCode);
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
                    ddlCountryCodes.Enabled = false;
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
            dtmEventDate.Value = ec?.EventDate ?? DateTime.Now;
            if (ec is not null)
                ddlCountryCodes.SelectedIndex = _viewModel.GetCountryCodeIndex(ec.CountryCode);
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

        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        void IFormControl.Resize(Control parentControl)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        private void dtmEventDate_ValueChanged(object sender, EventArgs e)
        {
            if (dtmEventDate.Enabled)
            {
                var countryCode = _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? string.Empty;
                _viewModel.LoadEconomicCalendars(DateOnly.FromDateTime(dtmEventDate.Value), countryCode);
            }
        }

        private void txtActual_TextChanged(object sender, EventArgs e)
        {

        }
    }

}
