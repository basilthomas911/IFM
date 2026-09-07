using System.Globalization;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Fund;

namespace TomasAI.IFM.UI.Net.Views.Fund;

/// <summary>
/// Modal editor for an operator-entered cash deposit or withdrawal.
/// </summary>
public sealed class FundCashTransactionEditor : DarkTradingForm, IFormControl
{
    readonly FundCashTransactionViewModel _viewModel;
    readonly TextBox _amount = new() { Name = "txtAmount", Dock = DockStyle.Fill };
    readonly TextBox _description = new() { Name = "txtDescription", Dock = DockStyle.Fill };
    readonly Button _save = new() { Name = "btnSave", Text = "Save", AutoSize = true };
    bool _listenerReady;

    public FundCashTransactionEditor(FundCashTransactionViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Name = nameof(FundCashTransactionEditor);
        Text = viewModel.TransactionType == FundTransactionType.CashDeposit
            ? "Create Cash Deposit"
            : "Create Cash Withdrawal";
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Name = "cashTransactionLayout",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(12),
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        AddReadOnlyRow(layout, 0, "Fund:", "txtFundName", viewModel.Fund.Name);
        AddReadOnlyRow(layout, 1, "Transaction:", "txtTransactionType", viewModel.TransactionType.ToString());
        AddReadOnlyRow(layout, 2, "Value date:", "txtValueDate", viewModel.ValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddReadOnlyRow(layout, 3, "Current balance:", "txtBalance", viewModel.Fund.Balance.ToString("C", CultureInfo.CurrentCulture));
        AddRow(layout, 4, "Amount:", _amount);
        AddRow(layout, 5, "Description:", _description);

        var buttons = new FlowLayoutPanel
        {
            Name = "pnlButtons",
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var cancel = new Button { Name = "btnCancel", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_save);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = _save;
        CancelButton = cancel;

        _amount.TextChanged += (_, _) => UpdateSaveEnabled();
        _description.TextChanged += (_, _) => UpdateSaveEnabled();
        _save.Click += SaveAsync;
        Load += LoadAsync;
        FormClosed += ClosedAsync;
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        UpdateSaveEnabled();
    }

    async void LoadAsync(object? sender, EventArgs eventArgs)
    {
        try
        {
            await _viewModel.InitializeAsync(CancellationToken.None);
            _listenerReady = true;
            UpdateSaveEnabled();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Fund Cash Transaction Error");
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    async void SaveAsync(object? sender, EventArgs eventArgs)
    {
        if (!decimal.TryParse(_amount.Text, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.CurrentCulture, out var amount))
            return;
        try
        {
            _viewModel.SetPendingTransaction(_viewModel.CreateTransaction(amount, _description.Text));
            UpdateSaveEnabled();
            await _viewModel.SubmitOperation.ExecuteAsync();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Fund Cash Transaction Error");
            UpdateSaveEnabled();
        }
    }

    async void ClosedAsync(object? sender, FormClosedEventArgs eventArgs)
    {
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        try
        {
            await _viewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            await _viewModel.WriteStatusConsole(
                LogSourceType.Fund,
                exception.HResult,
                $"Stopping the fund cash transaction listener failed: {exception.Message}");
        }
    }

    void ViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (IsDisposed)
            return;
        this.Post(() =>
        {
            if (_viewModel.IsCompleted)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            if (_viewModel.Failure is { } failure)
                this.ShowErrorMessage(failure.Message, "Fund Cash Transaction Error");
            UpdateSaveEnabled();
        });
    }

    void UpdateSaveEnabled()
        => _save.Enabled = _listenerReady
            && _viewModel.CommandId == Guid.Empty
            && !_viewModel.SubmitOperation.IsRunning
            && decimal.TryParse(_amount.Text, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.CurrentCulture, out var amount)
            && amount > 0m
            && !string.IsNullOrWhiteSpace(_description.Text);

    static void AddReadOnlyRow(TableLayoutPanel layout, int row, string label, string name, string value)
        => AddRow(layout, row, label, new TextBox
        {
            Name = name,
            Text = value,
            ReadOnly = true,
            Dock = DockStyle.Fill
        });

    static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    public void Open() => ShowDialog();
    void IFormControl.Resize(Control parentControl) { }
}
