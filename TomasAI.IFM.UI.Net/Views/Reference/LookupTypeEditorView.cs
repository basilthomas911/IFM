using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.ViewModels.Reference;

namespace TomasAI.IFM.Views.Reference;

public partial class LookupTypeEditorView
    : UserControl, IControlCommand, IFormControl
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

        _viewModel.OnLookupTypeNamesLoaded = () => this.Post(() =>
        {
            _canChangeRemove = false;
            lstLookupTypeNames.Items.Clear();
            if (_viewModel.LookupTypeNames?.Count > 0)
            {
                foreach (var e in _viewModel.LookupTypeNames)
                    lstLookupTypeNames.Items.Add($"{e}");
                lstLookupTypeNames.SelectedIndex = 0;
                _canChangeRemove = true;
            }
            dataLoaded?.Invoke(_canChangeRemove);
        });

        _viewModel.OnLookupTypeShortCodesLoaded = () => this.Post(() =>
        {
            lstLookupTypeShortCodes.Items.Clear();
            if (_viewModel.LookupTypeShortCodes?.Count > 0)
            {
                foreach (var e in _viewModel.LookupTypeShortCodes)
                    lstLookupTypeShortCodes.Items.Add(e.ShortCode);
                lstLookupTypeShortCodes.SelectedIndex = 0;
            }
        });

        _viewModel.OnLookupTypeLoaded = e => this.Post(() =>
        {
            txtLookupTypeName.Text = e?.LookupTypeName ?? String.Empty;
            txtShortCode.Text = e?.ShortCode ?? String.Empty;
            txtOrderId.Text = e != null ? $"{e.OrderId}" : string.Empty;
            txtDescription.Text = e?.Description ?? String.Empty;
            SetReadOnlyControls(true);
        });

        _viewModel.OnWaitCursor = () => this.Post(() => Cursor = Cursors.WaitCursor);
        _viewModel.OnDefaultCursor = () => this.Post(() => Cursor = Cursors.Default);

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
                txtLookupTypeName.Text = String.Empty;
                txtShortCode.Text = String.Empty;
                txtOrderId.Text = String.Empty;
                txtDescription.Text = String.Empty;
                SetReadOnlyControls(false);
                _editMode = EditMode.Add;
                addAction(false);
                lstLookupTypeNames.Enabled = false;
                lstLookupTypeShortCodes.Enabled = false;
                break;
            case EditMode.Add:
                var lookupType = new LookupTypeReadModel
                (
                    lookupTypeName: txtLookupTypeName.Text,
                    shortCode: txtShortCode.Text,
                    orderId: _viewModel.LookupTypeShortCodes?.Count ?? 0,
                    description: txtDescription.Text,
                    createdOn: DateTime.Now,
                    createdBy: String.Empty
                );
                _viewModel.AddLookupType(lookupType, () => this.Post(() =>
                {
                    _editMode = EditMode.View;
                    lstLookupTypeNames.Enabled = true;
                    lstLookupTypeShortCodes.Enabled = true;
                    addAction(true);
                }));
                break;
        }
    }

    /// <summary>
    /// Handles the process of changing a lookup type based on the current edit mode.
    /// </summary>
    /// <remarks>This method validates the input for the Order ID, determines the appropriate action based on
    /// the current edit mode,  and either transitions to edit mode or applies the changes to the lookup type.  If the
    /// Order ID is invalid, an error message is displayed, and the operation is aborted.</remarks>
    /// <param name="changeAction">A callback action that is invoked with a <see langword="true"/> if the change operation completes successfully, 
    /// or <see langword="false"/> if the operation transitions to edit mode without completing the change.</param>
    public void Change(Action<bool> changeAction)
    {
        var lookupTypeName = txtLookupTypeName.Text;
        var orderIdValue = txtOrderId.Text;
        if (!int.TryParse(orderIdValue, out var orderId) || orderId < 0)
        {
            MessageBox.Show("Order ID must be a non-negative integer.", "Invalid Order ID", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var lookupTypeId = _viewModel.GetLookupType(lookupTypeName, orderId)?.Id;
        if (lookupTypeId != null)
        {
            switch (_editMode)
            {
                case EditMode.View:
                    SetReadOnlyControls(false);
                    _editMode = EditMode.Change;
                    changeAction(false);
                    lstLookupTypeNames.Enabled = false;
                    lstLookupTypeShortCodes.Enabled = false;
                    break;
                case EditMode.Change:
                    var lookupType = new LookupTypeReadModel
                    (
                        lookupTypeName: txtLookupTypeName.Text,
                        shortCode: txtShortCode.Text,
                        orderId: Convert.ToInt32(txtOrderId.Text),
                        description: txtDescription.Text,
                        createdOn: DateTime.Now,
                        createdBy: String.Empty
                    );
                    _viewModel.ChangeLookupType(lookupTypeId, lookupType, true, () => this.Post(() =>
                    {
                        _editMode = EditMode.View;
                        lstLookupTypeNames.Enabled = true;
                        lstLookupTypeShortCodes.Enabled = true;
                        changeAction(true);
                    }));
                    break;
            }
        }
    }

    public void Remove()
    {
        var lookupTypeName = txtLookupTypeName.Text;
        var orderIdValue = txtOrderId.Text;
        if (!int.TryParse(orderIdValue, out var orderId) || orderId < 0)
        {
            MessageBox.Show("Order ID must be a non-negative integer.", "Invalid Order ID", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var lookupTypeId = _viewModel.GetLookupType(lookupTypeName, orderId)?.Id;
        if (lookupTypeId != null)
            if (MessageBox.Show($"Are you sure you want to remove Lookup Type {lookupTypeId} ?", "Remove Lookup Type", MessageBoxButtons.YesNo) == DialogResult.Yes)
                _viewModel.RemoveLookupType(lookupTypeId, true);
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
                lstLookupTypeNames.Enabled = true;
                lstLookupTypeShortCodes.Enabled = true;
                SetReadOnlyControls(true);
                return false;
        }
        return true;
    }

    public void Import()
    {
    }

    enum EditMode
    {
        View,
        Add,
        Change
    }

    void SetReadOnlyControls(bool readOnly)
    {
        txtLookupTypeName.ReadOnly = readOnly;
        txtShortCode.ReadOnly = readOnly;
        txtDescription.ReadOnly = readOnly;
        txtLookupTypeName.BorderStyle = readOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
        txtShortCode.BorderStyle = readOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
        txtDescription.BorderStyle = readOnly ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
    }

    void lstLookupTypeNames_SelectedIndexChanged(object sender, EventArgs e)
    {
        var lookupTypeName = _viewModel.GetLookupTypeName(lstLookupTypeNames.SelectedIndex);
        _viewModel.LoadLookupTypeShortCodes(lookupTypeName);
    }

    void lstLookupTypeShortCodes_SelectedIndexChanged(object sender, EventArgs e)
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

    private void txtDescription_TextChanged(object sender, EventArgs e)
    {

    }
}
