using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Models.Reference;

namespace TomasAI.IFM.UI.Net.Views.Reference;

public partial class LookupTypeEditorView
    : UserControl, IControlCommand, IAsyncFormControl
{
    readonly LookupTypeEditorViewModel _viewModel;
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

        _ = LoadEditorAsync();
    }

    public void Unload() => _ = ((IAsyncFormControl)this).CloseAsync();

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
                var lookupType = new LookupTypeUiModel(
                    txtLookupTypeName.Text,
                    txtShortCode.Text,
                    _viewModel.GetNextOrderId(txtLookupTypeName.Text),
                    txtDescription.Text,
                    DateTime.UtcNow,
                    string.Empty);
                ObserveMutation(_viewModel.AddLookupType(lookupType, () => this.Post(() =>
                {
                    _editMode = EditMode.View;
                    lstLookupTypeNames.Enabled = true;
                    lstLookupTypeShortCodes.Enabled = true;
                    addAction(true);
                })), "Lookup Type Add Failed");
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
        var selectedLookupType = _viewModel.GetLookupType(lookupTypeName, orderId);
        if (selectedLookupType != null)
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
                    var lookupType = new LookupTypeUiModel
                    (
                        LookupTypeName: txtLookupTypeName.Text,
                        ShortCode: txtShortCode.Text,
                        OrderId: Convert.ToInt32(txtOrderId.Text),
                        Description: txtDescription.Text,
                        CreatedOn: DateTime.UtcNow,
                        CreatedBy: String.Empty
                    );
                    ObserveMutation(_viewModel.ChangeLookupType(
                        selectedLookupType.LookupTypeName,
                        selectedLookupType.OrderId,
                        lookupType,
                        true,
                        () => this.Post(() =>
                    {
                        _editMode = EditMode.View;
                        lstLookupTypeNames.Enabled = true;
                        lstLookupTypeShortCodes.Enabled = true;
                        changeAction(true);
                    })), "Lookup Type Change Failed");
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
        var selectedLookupType = _viewModel.GetLookupType(lookupTypeName, orderId);
        if (selectedLookupType != null)
            if (MessageBox.Show($"Are you sure you want to remove Lookup Type {lookupTypeName}:{orderId} ?", "Remove Lookup Type", MessageBoxButtons.YesNo) == DialogResult.Yes)
                ObserveMutation(
                    _viewModel.RemoveLookupType(lookupTypeName, orderId, true),
                    "Lookup Type Remove Failed");
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

    async Task LoadEditorAsync()
    {
        try
        {
            await _viewModel.LoadLookupTypes();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Lookup Type Editor Error");
        }
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

    private void txtDescription_TextChanged(object sender, EventArgs e)
    {

    }
}
