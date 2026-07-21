using System;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade
{
    public partial class TradeEndOfDayForm : Form, IFormControl
    {
        private readonly IAppRoot _appRoot;
        private readonly EndOfDayProcessViewModel _viewModel;

        public TradeEndOfDayForm(IAppRoot appRoot, TradeEndOfDayParameter eodParam)
        {
            _appRoot = appRoot;
            _viewModel = new EndOfDayProcessViewModel(appRoot, eodParam);
            InitializeComponent();
        }

        void TradeEndOfDayForm_Load(object sender, EventArgs e)
        {
            txtFundId.Text = $"{_viewModel.FundId}";
            txtOrderId.Text = $"{_viewModel.OrderId}";
            txtTradeId.Text = $"{_viewModel.TradeId}";
            dtpValueDate.Enabled = false;
            dtpValueDate.Value = _viewModel.ValueDate.ToDateTime(TimeOnly.MinValue);
            dtpValueDate.Enabled = true;
            _viewModel.StartListener();
        }

        void TradeEndOfDayForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _viewModel.StopListener();
        }

        void btnRun_Click(object sender, EventArgs e)
        {
            btnRun.Enabled = false;
            btnLoad.Enabled = false;
            _viewModel.Reference = txtReference.Text;
            _viewModel.OnEndOfDayProcessCompleted = () => this.Post(() => {
                DialogResult = DialogResult.OK;
                this.Close();
            });
            _viewModel.OnEndOfDayProcessFailed = (errorMsg) => this.Post(() => {
                MessageBox.Show(errorMsg, "End Of Day Process Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                this.Close();
            });
            _viewModel.RunEndOfDayProcess();
        }

        void btnClose_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        }

        void dtpValueDate_ValueChanged(object sender, EventArgs e) => LoadData();

        void IFormControl.Resize(Control parentControl)
        {
            throw new NotImplementedException();
        }

        void btnLoad_Click(object sender, EventArgs e) => LoadData();

        void LoadData()
        {
            btnRun.Enabled = false;
            txtOpenPrice.Text = string.Empty;
            txtHighPrice.Text = string.Empty;
            txtLowPrice.Text = string.Empty;
            txtClosePrice.Text = string.Empty;
            txtVolume.Text = string.Empty;
            txtTradePnl.Text = string.Empty;
            if (!dtpValueDate.Enabled) 
                return;
            _viewModel.ValueDate = new DateOnly(dtpValueDate.Value.Year, dtpValueDate.Value.Month, dtpValueDate.Value.Day);
            _viewModel.LoadData(() => this.Post(() => {
                txtOpenPrice.Text = $"{_viewModel.OpenPrice:###0.00}";
                txtHighPrice.Text = $"{_viewModel.HighPrice:###0.00}";
                txtLowPrice.Text = $"{_viewModel.LowPrice:###0.00}";
                txtClosePrice.Text = $"{_viewModel.ClosePrice:###0.00}";
                txtVolume.Text = $"{_viewModel.Volume:#,###,##0}";
                txtTradePnl.Text = $"{_viewModel.TradePnl:C}";
                btnRun.Enabled = true;
            }));
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

    }
}
