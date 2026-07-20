using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.ViewModels.Trade;

namespace TomasAI.IFM.Views.Trade
{
    public partial class CreateFundOrderTradeForm : Form, IForm<CreateFundOrderTradeForm>, IFormControl
    {
        readonly IAppRoot _appRoot;
        TradeOrderEditorViewModel _viewModel;
        FundOrderTradeReadModel _fundOrderTrade;
        Dictionary<string, LookupTypeReadModel> _baseSymbolMap;

        public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade;

        public CreateFundOrderTradeForm(IAppRoot appRoot)
        {
            _appRoot = appRoot;
            _baseSymbolMap = new Dictionary<string, LookupTypeReadModel>();
            InitializeComponent();
          }

        public void SetViewModel(TradeOrderEditorViewModel viewModel) => _viewModel = viewModel;

        public void SetFundOrder(FundOrderReadModel fundOrder)
        {
            dtpTradeDate.Value = fundOrder.TradeDate;
            dtpTradeDate.Enabled = false;
            dtpMaturityDate.Value = fundOrder.MaturityDate;
            dtpMaturityDate.Enabled = false;    
        }

        private void LoadTradeTypes()
        {
            ddlTradeType.Enabled = false;
            ddlTradeType.Items.Clear();
            ddlTradeType.Items.Add($"{TradeType.ShortIronCondor}");
            ddlTradeType.Items.Add($"{TradeType.LongIronCondor}");
            ddlTradeType.SelectedIndex = 0;
            ddlTradeType.Enabled = true;
        }

        private void CreateFundOrderTradeForm_Load(object sender, EventArgs e)
        {
            _appRoot.GetModel<ReferenceQueryModel>().Execute(async model =>
            {
                await model.GetCurrentTradeIdAsync(currentTradeId =>
                    this.Post(() =>
                    {
                        txtTradeId.Text = $"{currentTradeId + 1}";
                        txtTradeState.Text = $"{TradeState.NewTrade}";
                        LoadTradeTypes();
                        var openingFundOrderTrade = _viewModel.GetOpeningFundOrderTrade();
                        if (openingFundOrderTrade != null)
                        {
                            SetClosingTradeType(openingFundOrderTrade.TradeType);
                            txtReference.Text = openingFundOrderTrade.Reference;
                        }
                    }));

                await model.LoadSymbolsAsync(symbols =>
                    this.Post(() => LoadSymbols(symbols)));
            });
            return;

            void SetClosingTradeType(TradeType openingTradeType)
            {
                var closingTradeType = openingTradeType switch
                {
                    TradeType.ShortIronCondor => TradeType.LongIronCondor,
                    TradeType.LongIronCondor => TradeType.ShortIronCondor,
                    _ => throw new NotImplementedException()
                };
                for (var index = 0; index < ddlTradeType.Items.Count; index++)
                    if ($"{ddlTradeType.Items[index]}" == $"{closingTradeType}")
                    {
                        ddlTradeType.SelectedIndex = index;
                        break;
                    }
            }
        }

        private void CreateFundOrderTradeForm_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void LoadSymbols(LookupTypeReadModel[] lookupTypes)
        {
            ddlBaseSymbol.Enabled = false;
            ddlBaseSymbol.Items.Clear();
            if (lookupTypes?.Length > 0)
            {
                foreach (var e in lookupTypes)
                {
                    _baseSymbolMap.Add(e.Description, e);
                    ddlBaseSymbol.Items.Add(e.Description);
                }
                ddlBaseSymbol.SelectedIndex = 0;
                ddlBaseSymbol.Enabled = true;
            }
        }

        private FundOrderTradeReadModel ValidateNewFundOrderTrade()
        {
            if (!int.TryParse(txtTradeId.Text, out int tradeId))
            {
                MessageBox.Show("Invalid Trade Id", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            if (!Enum.TryParse($"{ddlTradeType.SelectedItem ?? string.Empty}", out TradeType tradeType))
            {
                MessageBox.Show("Invalid Trade Type", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            if (!Enum.TryParse(txtTradeState.Text, out TradeState tradeState))
            {
                MessageBox.Show("Invalid Trade State", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            if (!Enum.TryParse(txtTradeAction.Text, out TradeAction tradeAction))
            {
                MessageBox.Show("Invalid Trade Action", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var lookupType = _baseSymbolMap.SingleOrDefault(e => e.Key == $"{ddlBaseSymbol.SelectedItem}").Value;
            if (lookupType is null)
            {
                MessageBox.Show("Invalid Base Symbol", "Fund Order Trade Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            return new FundOrderTradeReadModel(
                FundId: 0,
                OrderId: 0,
                TradeId: tradeId,
                TradeType: tradeType,
                TradeDate: dtpTradeDate.Value,
                MaturityDate: dtpMaturityDate.Value,
                TradeState: tradeState,
                TradeAction: tradeAction,
                Reference: txtReference.Text,
                PrimaryTrade: true,
                BaseContractSymbol: lookupType.ShortCode,
                CreatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
                CreatedOn: DateTime.Now,
                UpdatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
                UpdatedOn: DateTime.Now
            );
        }

     
        

        private void btnSave_Click(object sender, EventArgs e)
        {
            _fundOrderTrade = ValidateNewFundOrderTrade();
            if (_fundOrderTrade is not null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _fundOrderTrade = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ddlTradeType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var tradeType = (TradeType)Enum.Parse(typeof(TradeType), $"{ddlTradeType.SelectedItem}");
            switch(tradeType)
            {
                case TradeType.ShortIronCondor:
                    txtTradeAction.Text = $"{TradeAction.Sell}";
                    break;
                case TradeType.LongIronCondor:
                    txtTradeAction.Text = $"{TradeAction.Buy}";
                    break;
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
