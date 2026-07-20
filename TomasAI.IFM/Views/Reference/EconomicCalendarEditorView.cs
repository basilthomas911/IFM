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
        int _lastCalendarEventIndex;
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

            _viewModel.OnEconomicCalendarsLoaded = () => this.Post(() => {
                _canChangeRemove = false;
                lstCalendarEvents.Items.Clear();
                if ((_viewModel.EconomicCalendars?.Count ?? 0) > 0)
                {
                    foreach (var ec in _viewModel.EconomicCalendars)
                        lstCalendarEvents.Items.Add($"{ec.Id}");
                    lstCalendarEvents.SelectedIndex = 0;
                    _canChangeRemove = true;
                }
                dataLoaded?.Invoke(_canChangeRemove);
            });

            _viewModel.OnCountryCodesLoaded = () => this.Post(() => {
                ddlCountryCodes.Items.Clear();
                 if ((_viewModel.CountryCodes?.Count ?? 0) == 0) 
                    return;
                foreach (var e in _viewModel.CountryCodes)
                    ddlCountryCodes.Items.Add(e.CountryCode);
                var selectedIndex = ddlCountryCodes.Items.IndexOf("US");
                ddlCountryCodes.SelectedIndex = selectedIndex == -1 ? 0 : selectedIndex;
            });

            _viewModel.LoadCountryCodes();
            _viewModel.LoadEconomicCalendars();
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
        public void Add( Action<bool> addAction)
        {
            switch (_editMode)
            {
                case EditMode.View:
                    dtmEventDate.Value = DateTime.Now;
                    dtmEventDate.Enabled = true;
                    ddlCountryCodes.SelectedIndex = 0;
                    ddlCountryCodes.Enabled = true;
                    txtEventName.Enabled = true;
                    txtEventName.Text = String.Empty;
                    txtActual.Enabled = true;
                    txtActual.Text = String.Empty;
                    txtForecast.Enabled = true;
                    txtForecast.Text = String.Empty;
                    txtPrior.Enabled = true;
                    txtPrior.Text = String.Empty;
                    _lastCalendarEventIndex = lstCalendarEvents.SelectedIndex;
                    _editMode = EditMode.Add;
                    addAction(false);
                    break;
                case EditMode.Add:
                    var economicCalendar = new EconomicCalendarReadModel
                    (
                        EventDate: dtmEventDate.Value,
                        CountryCode: _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? String.Empty,
                        EventName: txtEventName.Text,
                        Actual: txtActual.Text,
                        Forecast: txtForecast.Text,
                        Prior: txtPrior.Text,
                        CreatedOn: DateTime.Now,
                        CreatedBy: String.Empty
                    );
                    _viewModel.AddEconomicCalendar(economicCalendar, () => this.Post(() => {
                        _editMode = EditMode.View;
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
                        ddlCountryCodes.SelectedIndex = _viewModel.GetCountryCodeIndex(economicCalendarId.CountryCode);
                        ddlCountryCodes.Enabled = false;
                        txtEventName.Enabled = false;
                        txtActual.Enabled = true;
                        txtForecast.Enabled = true;
                        txtPrior.Enabled = true;
                        _lastCalendarEventIndex = lstCalendarEvents.SelectedIndex;
                        _editMode = EditMode.Change;
                        changeAction(false);
                        break;
                    case EditMode.Change:
                        var economicCalendar = new EconomicCalendarReadModel
                        (
                           EventDate: economicCalendarId.EventDate,
                           CountryCode: _viewModel.GetCountryCode(ddlCountryCodes.SelectedIndex) ?? String.Empty,
                           EventName: txtEventName.Text,
                           Actual: txtActual.Text,
                           Forecast: txtForecast.Text,
                           Prior: txtPrior.Text,
                           CreatedOn: DateTime.Now,
                           CreatedBy: String.Empty
                        );
                        _viewModel.ChangeEconomicCalendar(economicCalendarId, economicCalendar, () => this.Post(() => {
                            _editMode = EditMode.View;
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
                    _viewModel.RemoveEconomicCalendar(economicCalendarId);
        }

        public void Import()
        {
            _viewModel.ImportEconomicCalendars(DateTime.Now.Date);
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
                    return false;
            }
            return true;
        }

        /// <summary>
        /// return index value for lookup type short code
        /// </summary>
        /// <param name="lookupTypes"></param>
        /// <param name="shortCode"></param>
        /// <returns></returns>
        private int GetCountryCodeIndex(ICollection<EconomicCalendarCountryCodeReadModel> countryCodes, string countryCode)
        {
            for(var index = 0; index < countryCodes.Count; index++)
                if (countryCodes.ElementAt(index).CountryCode == countryCode)
                    return index;
            return -1;
        }

        /// <summary>
        /// show selected futures contract details
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lstCalendarEvents_SelectedIndexChanged(object sender, EventArgs e)
            => ShowSelectedEconomicCalendar(lstCalendarEvents.SelectedIndex);

        /// <summary>
        /// show futures contract details
        /// </summary>
        /// <param name="selectedIndex"></param>
        private void ShowSelectedEconomicCalendar(int selectedIndex)
        {
            var ec = _viewModel.GetEconomicCalendar(selectedIndex);
            dtmEventDate.Value = ec?.EventDate ?? DateTime.Now;
            dtmEventDate.Enabled = false;
            if (ec != null)
                ddlCountryCodes.SelectedIndex = _viewModel.GetCountryCodeIndex(ec.CountryCode);
            ddlCountryCodes.Enabled = false;
            txtEventName.Text = ec.EventName ?? String .Empty;
            txtEventName.Enabled = false;
            txtActual.Text = ec.Actual ?? String.Empty;
            txtActual.Enabled = false;
            txtForecast.Text = ec.Forecast ?? String.Empty;
            txtForecast.Enabled = false;
            txtPrior.Text = ec.Prior ?? String.Empty;
            txtPrior.Enabled = false;
        }

        private enum EditMode
        {
            View,
            Add,
            Change
        }

       
        private void ddlCountryCodes_SelectedIndexChanged(object sender, EventArgs e)
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
    }
    
}
