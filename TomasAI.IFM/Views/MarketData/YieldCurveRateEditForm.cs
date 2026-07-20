using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Models;
using TomasAI.IFM.Contracts;

namespace TomasAI.IFM.Views.MarketData
{
    public partial class YieldCurveRateEditForm : Form, IForm<YieldCurveRateEditForm>, IFormControl
    {
        private IAppRoot _appRoot;
        private YieldCurveRateReadModel _yieldCurveRate;

        public YieldCurveRateReadModel YieldCurveRate => _yieldCurveRate;

        public YieldCurveRateEditForm(IAppRoot appRoot)
        {
            _appRoot = appRoot;
            InitializeComponent();
        }

        public void SetYieldCurveRate(YieldCurveRateReadModel yieldCurveRate) => _yieldCurveRate = yieldCurveRate;
        
        private void YieldCurveRateEditForm_Load(object sender, EventArgs e)
        {
            ShowYieldCurveRate();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (ValidateOnSave())
                this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _yieldCurveRate = null;
            this.Close();
        }

        private void ShowYieldCurveRate()
        {
            if (_yieldCurveRate == null|| _yieldCurveRate.ValueDate == DateTime.MinValue)
            {
                this.Text = "Add Yield Curve Rate";
                dtmValueDate.Value = DateTime.Now;
            }
            else
            {
                this.Text = "Change Yield Curve Rate";
                dtmValueDate.Value = _yieldCurveRate.ValueDate;
                dtmValueDate.Enabled = false;
                txtOneMonth.Text = $"{_yieldCurveRate.OneMonth:F2}";
                txtTwoMonth.Text = $"{_yieldCurveRate.TwoMonth:F2}";
                txtThreeMonth.Text = $"{_yieldCurveRate.ThreeMonth:F2}";
                txtSixMonth.Text = $"{_yieldCurveRate.SixMonth:F2}";
                txtOneYear.Text = $"{_yieldCurveRate.OneYear:F2}";
                txtTwoYear.Text = $"{_yieldCurveRate.TwoYear:F2}";
                txtThreeYear.Text = $"{_yieldCurveRate.ThreeYear:F2}";
                txtFiveYear.Text = $"{_yieldCurveRate.FiveYear:F2}";
                txtSevenYear.Text = $"{_yieldCurveRate.SevenYear:F2}";
                txtTenYear.Text = $"{_yieldCurveRate.TenYear:F2}";
                txtTwentyYear.Text = $"{_yieldCurveRate.TwentyYear:F2}";
                txtThirtyYear.Text = $"{_yieldCurveRate.ThirtyYear:F2}";
            }
        }

        private bool ValidateOnSave()
        {
            if (!ValidateRate(txtOneMonth.Text, out var oneMonth, "Invalid 1 Month rate") ||
                !ValidateRate(txtTwoMonth.Text, out var twoMonth, "Invalid 2 Month rate") ||
                !ValidateRate(txtThreeMonth.Text, out var threeMonth, "Invalid 3 Month rate") ||
                !ValidateRate(txtSixMonth.Text, out var sixMonth, "Invalid 6 Month rate") ||
                !ValidateRate(txtOneYear.Text, out var oneYear, "Invalid 1 Year rate") ||
                !ValidateRate(txtTwoYear.Text, out var twoYear, "Invalid 2 Year rate") ||
                !ValidateRate(txtThreeYear.Text, out var threeYear, "Invalid 3 Year rate") ||
                !ValidateRate(txtFiveYear.Text, out var fiveYear, "Invalid 5 Year rate") ||
                !ValidateRate(txtSevenYear.Text, out var sevenYear, "Invalid 7 Year rate") ||
                !ValidateRate(txtTenYear.Text, out var tenYear, "Invalid 10 Year rate") ||
                !ValidateRate(txtTwentyYear.Text, out var twentyYear, "Invalid 20 Year rate") ||
                !ValidateRate(txtThirtyYear.Text, out var thirtyYear, "Invalid 30 Year rate"))
                return false;
            _yieldCurveRate = new YieldCurveRateReadModel (
                ValueDate: new DateTime(dtmValueDate.Value.Year, dtmValueDate.Value.Month, dtmValueDate.Value.Day),
                OneMonth: oneMonth,
                TwoMonth: twoMonth,
                ThreeMonth: threeMonth,
                SixMonth: sixMonth,
                OneYear: oneYear,
                TwoYear: twoYear,
                ThreeYear: threeYear,
                FiveYear: fiveYear,
                SevenYear: sevenYear,
                TenYear: tenYear,
                TwentyYear: twentyYear,
                ThirtyYear: thirtyYear
            );
            return true;
        }

        private bool ValidateRate(string value, out double rate, string errorMessage)
        {
            if (!double.TryParse(value, out rate))
            {
                MessageBox.Show(errorMessage, "Validate Year Curve Rates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private void dtmValueDate_ValueChanged(object sender, EventArgs e)
        {
            var valueDate = new DateTime(dtmValueDate.Value.Year, dtmValueDate.Value.Month, dtmValueDate.Value.Day);
            if (_yieldCurveRate == null)
            {
                _appRoot.GetModel<MarketDataQueryModel>().Execute(async ctlr => {
                    ctlr.OnError((errorCode, errorMsg) => this.Post(() => MessageBox.Show(text: errorMsg, caption: "Yield Curve Rate Error", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Error)));
                    await ctlr.YieldCurveRateExistsAsync(valueDate, serviceResult =>
                        this.Post(() => {
                            btnSave.Enabled = false;
                            if (!serviceResult.Success)
                                MessageBox.Show($"Yield Curve Rate data error : {serviceResult.ErrorMessage}", "Validate Year Curve Rates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            else if (serviceResult.Value.Value)
                                MessageBox.Show($"Yield Curve Rate already exists for : {valueDate:yyyy-MMM-dd}", "Validate Year Curve Rates", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            else
                                btnSave.Enabled = true;
                        }));
                });
            }
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        void IFormControl.Resize(Control parentControl)
        {
            throw new NotImplementedException();
        }
       
    }
    
}
