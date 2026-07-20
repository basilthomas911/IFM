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
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.ViewModels.Reference;

namespace TomasAI.IFM.Views.Reference
{
    public partial class ReferenceView : Form, IForm<ReferenceView>, IFormControl
    {
        readonly IAppRoot _appRoot;
        ReferenceViewModel _viewModel;
        IControlCommand _ctrlCommand;
        Dictionary<string, Func<IAppRoot,Control>> _controlMap;

        public ReferenceView(IAppRoot appRoot)
        {
            _appRoot = appRoot;
            _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
            {
                { "EconomicCalendar", ar => new EconomicCalendarEditorView( new EconomicCalendarEditorViewModel(ar) )},
                { "LookupTypes", ar => new LookupTypeEditorView( new LookupTypeEditorViewModel(ar) )}
            };
            _ctrlCommand = null;
            InitializeComponent();
        }

        /// <summary>
        /// load reference view model
        /// </summary>
        /// <param name="viewModel"></param>
        public void LoadViewModel(ReferenceViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// load reference view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReferenceForm_Load(object sender, EventArgs e)
        {
            _viewModel.LoadReferenceDataDefinitionTypes(mktDataDefTypes =>
              this.Post(() => {
                  ddlReferenceDataSelector.Items.Clear();
                  if (mktDataDefTypes != null && mktDataDefTypes.Length > 0)
                  {
                      foreach (var mktDataDefType in mktDataDefTypes)
                          ddlReferenceDataSelector.Items.Add(mktDataDefType.Description);
                      ddlReferenceDataSelector.SelectedIndex = 0;
                  }
              }));
        }

        private void ReferenceForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _ctrlCommand?.Unload();
            ResetButtons(false);
        }

        private void ddlReferenceDataSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetButtons(false);
            _ctrlCommand?.Unload();
            pnlMarketData.Controls.Clear();
            var mktDataDefType = _viewModel.GetMarketDefinitionType(ddlReferenceDataSelector.SelectedIndex);
            if (_controlMap.ContainsKey(mktDataDefType.ShortCode))
            {
                var control = _controlMap[mktDataDefType.ShortCode](_appRoot);
                control.Visible = false;
                pnlMarketData.Controls.Add(control);
                _ctrlCommand = control as IControlCommand;
                _ctrlCommand.Load(_appRoot, enabled => {
                    btnChange.Enabled = _ctrlCommand.CanChangeRemove;
                    btnRemove.Enabled = _ctrlCommand.CanChangeRemove;
                    btnImport.Enabled = _ctrlCommand.CanImport;
                });
                control.Visible = true;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e) => _ctrlCommand?.Add(enabled => RefreshAddButton(enabled));

        private void btnChange_Click(object sender, EventArgs e ) => _ctrlCommand?.Change(enabled => RefreshChangeButton(enabled));

        private void btnRemove_Click(object sender, EventArgs e) => _ctrlCommand.Remove();

        private void btnClose_Click(object sender, EventArgs e)
        {
            if (_ctrlCommand?.Close(enabled => ResetButtons(enabled)) ?? false)
                this.Close();
        }

        private void btnImport_Click(object sender, EventArgs e) => _ctrlCommand?.Import();

        private void RefreshAddButton(bool enabled)
        {
            btnAdd.Text = !enabled ? "Save" : "Add";
            btnChange.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClose.Text = !enabled ? "Cancel" : "Close";
        }

        private void RefreshChangeButton(bool enabled)
        {
            btnChange.Text = !enabled ? "Save" : "Change";
            btnAdd.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClose.Text = !enabled ? "Cancel" : "Close";
        }

        private void ResetButtons(bool enabled)
        {
            btnAdd.Text = @"&Add";
            btnAdd.Enabled = true;
            btnChange.Text = @"C&hange";
            btnChange.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClose.Text = "Close";
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
