using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.ViewModels.MarketData;
using TomasAI.IFM.Views.SystemInfo;
namespace TomasAI.IFM.Views.MarketData
{
    public partial class YieldCurveRateEditorControl : UserControl, IControlCommand, IFormControl
    {
        YieldCurveRateEditorViewModel _viewModel;

        public bool CanChangeRemove => _viewModel.CanChangeRemove;

        public bool CanImport => true;

        public YieldCurveRateEditorControl(YieldCurveRateEditorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            
        }

        void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
        {
            _viewModel.OnDataLoaded = dataLoaded;
            _viewModel.ShowWaitView = (e, message) => this.Post(() => (new SystemWaitView(e, message)).Show());
            _viewModel.OnError = (_, errorMsg) => this.Post(() =>
               MessageBox.Show(
                   text: errorMsg,
                   caption: "Yield Curve Rates Editor Error",
                   buttons: MessageBoxButtons.OK,
                   icon: MessageBoxIcon.Error));

             _viewModel.OnYieldCurveRateTimePeriodsLoaded = timePeriods =>
                       this.Post(() => {
                           ddlTimePeriod.DataSource = timePeriods;
                           ddlTimePeriod.SelectedIndex = 0;
                       });
            _viewModel.OnYieldCurveRateAdded = (ycr) => this.Post(() => {
                ShowYieldCurveRates();
                _viewModel.OnAddAction?.Invoke(true);
                _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"Yield Curve Rate for: {ycr.ValueDate:dd-MM-yyyy} added");
            });
            _viewModel.OnYieldCurveRateChanged = (ycr) => this.Post(() =>
            {
                ShowYieldCurveRates();
                _viewModel.OnChangeAction?.Invoke(true);
                _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"Yield Curve Rate for: {ycr.ValueDate:dd-MM-yyyy} changed");
            });
            _viewModel.OnYieldCurveRateRemoved = (valueDate) => this.Post(() =>
            {
                ShowYieldCurveRates();
                _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"Yield Curve Rate for: {valueDate:dd-MM-yyyy} was Removed");
            });

            _viewModel.OnYieldCurveRatesImported = (numImported) => this.Post(() => {
                ShowYieldCurveRates();
                _viewModel.WriteStatusConsole(LogSourceType.MarketData, $"{numImported} Yield Curve Rates Imported");
            });

            _viewModel.OnYieldCurveRatesLoaded = (yieldCurveRates) => this.Post(() => {
                _viewModel.CanChangeRemove = false;
                if (yieldCurveRates?.Length > 0)
                    _viewModel.CanChangeRemove = true;
                yieldCurveRatesBindingSource.DataSource = yieldCurveRates;
                gridYieldCurveRates.DataSource = yieldCurveRatesBindingSource;
                yieldCurveRatesBindingSource.ResetBindings(false);
                gridYieldCurveRates.Update();
                _viewModel.OnDataLoaded?.Invoke(yieldCurveRates.Length > 0);
                _viewModel.SetYieldCurveRates(yieldCurveRates);
            });

            _viewModel.OnShowYieldCurveRates = () => this.Post(() => {
                ShowYieldCurveRates();
            });
            _viewModel.LoadYieldCurveRateTimePeriods();
        }

        void IControlCommand.Unload()
        {
        }

        public void Add(Action<bool> addAction)
        {
            var dlg = new YieldCurveRateEditForm(_viewModel.AppRoot);
            switch (dlg.ShowDialog())
            {
                case DialogResult.OK:
                    _viewModel.OnAddAction = addAction;
                    _viewModel.AddYieldCurveRate(dlg.YieldCurveRate);
                    break;
                case DialogResult.Cancel:
                    addAction(true);
                    break;
            }
        }

        public void Change(Action<bool> changeAction)
        {
            var yieldCurveRate = _viewModel.GetYieldCurveRate(gridYieldCurveRates.SelectedRows[0].Index);
            var dlg = new YieldCurveRateEditForm(_viewModel.AppRoot);
            dlg.SetYieldCurveRate(yieldCurveRate);
            switch (dlg.ShowDialog())
            {
                case DialogResult.OK:
                    _viewModel.OnChangeAction = changeAction;
                    _viewModel.ChangeYieldCurveRate(dlg.YieldCurveRate);
                    break;
                case DialogResult.Cancel:
                    changeAction(true);
                    break;
            }
        }

        public bool Close(Action<bool> changeAction) => true;

        public void Remove()
        {
            var yieldCurveRate = _viewModel.GetYieldCurveRate(gridYieldCurveRates.SelectedRows[0].Index);
            if (MessageBox.Show($"Are you sure you want to remove the Yield Curve Rates for: {yieldCurveRate.ValueDate:yyyy-MMM-dd} ?"
                    , "Remove Yield Curve Rate", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _viewModel.RemoveYieldCurveRate(yieldCurveRate.ValueDate);
            }
        }

        public void Import() 
        {
            _viewModel.WriteStatusConsole(LogSourceType.MarketData, "Importing Yield Curve Rates...");
            _viewModel.ImportYieldCurveRates(DateTime.Now.Date);
        }
 
        private void ShowYieldCurveRates()
        {
            DateTime startDate;
            DateTime endDate;
            var timePeriod = $"{ddlTimePeriod.SelectedItem}";
            switch (timePeriod)
            {
                case "Current Month":
                    var currentDate = DateTime.Now;
                    startDate = new DateTime(currentDate.Year, currentDate.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
                default:
                    startDate = new DateTime(Convert.ToInt32(timePeriod), 1, 1);
                    endDate = startDate.AddYears(1).AddDays(-1);
                    break;
            }
            _viewModel.LoadYieldCurveRates(startDate, endDate);
        }

  

        private void ddlTimePeriod_SelectedIndexChanged(object sender, EventArgs e) => ShowYieldCurveRates();

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
