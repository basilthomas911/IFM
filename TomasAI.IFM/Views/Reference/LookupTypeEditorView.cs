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
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.ViewModels.Reference;

namespace TomasAI.IFM.Views.Reference
{
    public partial class LookupTypeEditorView : UserControl, IControlCommand, IFormControl
    {
        LookupTypeEditorViewModel _viewModel;
        EditMode _editMode;
        bool _canChangeRemove;
  
        public LookupTypeEditorView(LookupTypeEditorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
        }

        public bool CanChangeRemove => _canChangeRemove;

        public bool CanImport => false;

        void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
        {
            _editMode = EditMode.View;
            _viewModel.StartWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.WaitCursor);
            _viewModel.StopWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.Default);
            _viewModel.OnError = (_, errorMsg) => this.Post(() =>
                          MessageBox.Show(
                              text: errorMsg,
                              caption: "Lookup Type Editor Error",
                              buttons: MessageBoxButtons.OK,
                              icon: MessageBoxIcon.Error));

            _viewModel.OnLookupTypeNamesLoaded = () => this.Post(() => {
                _canChangeRemove = false;
                lstLookupTypeNames.Items.Clear();
                if ((_viewModel.LookupTypeNames?.Count ?? 0) > 0)
                {
                    foreach (var e in _viewModel.LookupTypeNames)
                        lstLookupTypeNames.Items.Add($"{e}");
                    lstLookupTypeNames.SelectedIndex = 0;
                    _canChangeRemove = true;
                }
                dataLoaded?.Invoke(_canChangeRemove);
            });

            _viewModel.OnLookupTypeShortCodesLoaded = () => this.Post(() => {
                lstLookupTypeShortCodes.Items.Clear();
                if ((_viewModel.LookupTypeShortCodes?.Count ?? 0) > 0)
                {
                    foreach (var e in _viewModel.LookupTypeShortCodes)
                        lstLookupTypeShortCodes.Items.Add(e.ShortCode);
                    lstLookupTypeShortCodes.SelectedIndex = 0;
                }
            });

            _viewModel.OnLookupTypeLoaded = e => this.Post(() => {
                txtLookupTypeName.Text = e?.LookupTypeName ?? String.Empty;
                txtShortCode.Text = e?.ShortCode ?? String.Empty;
                txtOrderId.Text = e != null ? $"{e.OrderId}" : string.Empty;
                txtDescription.Text = e?.Description ?? String.Empty;
            });

            _viewModel.LoadLookupTypes();
        }

        public void Unload()
        {
        }

        public void Add(Action<bool> addAction)
        {
            switch (_editMode)
            {
                case EditMode.View:
                    txtLookupTypeName.Enabled = true;
                    txtLookupTypeName.Text = String.Empty;
                    txtShortCode.Enabled = true;
                    txtShortCode.Text = String.Empty;
                    txtOrderId.Enabled = false;
                    txtOrderId.Text = String.Empty;
                    txtDescription.Enabled = true;
                    txtDescription.Text = String.Empty;
                    _editMode = EditMode.Add;
                    addAction(false);
                    break;
                case EditMode.Add:
                    var lookupType = new LookupTypeReadModel
                    (
                        LookupTypeName: txtLookupTypeName.Text,
                        ShortCode: txtShortCode.Text,
                        OrderId: _viewModel.LookupTypeShortCodes?.Count ?? 0,
                        Description: txtDescription.Text,
                        CreatedOn: DateTime.Now,
                        CreatedBy: String.Empty
                    );
                    _viewModel.AddLookupType(lookupType, () => this.Post(() =>
                    {
                        _editMode = EditMode.View;
                        addAction(true);
                    }));
                    break;
            }
        }

        public void Change(Action<bool> changeAction)
        {
            var lookupTypeName = txtLookupTypeName.Text;
            var shortCode = txtShortCode.Text;
            var lookupTypeId = _viewModel.GetLookupType(lookupTypeName, shortCode)?.Id;
            if (lookupTypeId != null)
            {
                switch (_editMode)
                {
                    case EditMode.View:
                        txtLookupTypeName.Enabled = true;
                        txtShortCode.Enabled = true;
                        txtOrderId.Enabled = false;
                        txtDescription.Enabled = true;
                        _editMode = EditMode.Change;
                        changeAction(false);
                        break;
                    case EditMode.Change:
                        var lookupType = new LookupTypeReadModel
                        (
                            LookupTypeName: txtLookupTypeName.Text,
                            ShortCode: txtShortCode.Text,
                            OrderId: Convert.ToInt32(txtOrderId.Text),
                            Description: txtDescription.Text,
                            CreatedOn: DateTime.Now,
                            CreatedBy: String.Empty
                        );
                        _viewModel.ChangeLookupType(lookupTypeId, lookupType, () => this.Post(() =>
                        {
                            _editMode = EditMode.View;
                            changeAction(true);
                        }));
                        break;
                }
            }
        }

        public void Remove()
        {
            var lookupTypeName = txtLookupTypeName.Text;
            var shortCode = txtShortCode.Text;
            var lookupTypeId = _viewModel.GetLookupType(lookupTypeName, shortCode)?.Id;
            if (lookupTypeId != null)
                if (MessageBox.Show($"Are you sure you want to remove Lookup Type {lookupTypeId} ?", "Remove Lookup Type", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    _viewModel.RemoveLookupType(lookupTypeId);
        }

        public bool Close(Action<bool> closeAction)
        {
            switch (_editMode)
            {
                case EditMode.Add:
                case EditMode.Change:
                    var lookupTypeName = _viewModel.GetLookupTypeName(lstLookupTypeNames.SelectedIndex);
                    var lookupTypeShortCode = _viewModel.GetLookupTypeShortCode(lstLookupTypeShortCodes.SelectedIndex);
                    _viewModel.LoadLookupType(lookupTypeName, lookupTypeShortCode);
                    _editMode = EditMode.View;
                    closeAction?.Invoke((_viewModel.LookupTypes?.Count ?? 0) > 0);
                    return false;
            }
            return true;
        }

        public void Import()
        {
        }
       
        private enum EditMode
        {
            View,
            Add,
            Change
        }

        private void lstLookupTypeNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            var lookupTypeName = _viewModel.GetLookupTypeName(lstLookupTypeNames.SelectedIndex);
            _viewModel.LoadLookupTypeShortCodes(lookupTypeName);
        }

        private void lstLookupTypeShortCodes_SelectedIndexChanged(object sender, EventArgs e)
        {
            var lookupTypeName = _viewModel.GetLookupTypeName(lstLookupTypeNames.SelectedIndex);
            var lookupTypeShortCode = _viewModel.GetLookupTypeShortCode(lstLookupTypeShortCodes.SelectedIndex);
            _viewModel.LoadLookupType(lookupTypeName, lookupTypeShortCode);
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
