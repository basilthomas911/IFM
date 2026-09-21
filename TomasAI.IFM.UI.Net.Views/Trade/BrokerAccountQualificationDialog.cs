using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Human-facing controls for the durable emulator account qualification gate.</summary>
public sealed class BrokerAccountQualificationDialog : DarkTradingForm
{
    private readonly BrokerManualTradeOrderViewModel _viewModel;
    private readonly Label _state = ValueLabel("Loading...");
    private readonly TextBox _manifest = Input("qualificationManifestHash");
    private readonly TextBox _evidence = Input("qualificationEvidenceReference");
    private readonly TextBox _reviewer = Input("qualificationReviewer", Environment.UserName);
    private readonly TextBox _reason = Input("qualificationReason");
    private readonly Button _submit = ActionButton("submitQualificationEvidence", "Submit evidence");
    private readonly Button _accept = ActionButton("acceptQualification", "Accept reviewed manifest");
    private readonly Button _revoke = ActionButton("revokeQualification", "Revoke");
    private readonly Button _hold = ActionButton("setTradingHold", "Set hold");
    private readonly Button _release = ActionButton("releaseTradingHold", "Release hold");
    private readonly Button _resynchronize = ActionButton("resynchronizeBrokerAccount", "Resynchronize");
    private BrokerAccountDefinition? _current;

    /// <summary>Creates the account qualification dialog without changing the current gate.</summary>
    public BrokerAccountQualificationDialog(BrokerManualTradeOrderViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Text = "Emulator Broker Account Qualification";
        Name = "BrokerAccountQualificationDialog";
        AccessibleName = "Emulator broker account qualification";
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Microsoft Sans Serif", 10F);
        MinimumSize = new Size(760, 520);
        Size = new Size(840, 600);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Color.Black, Padding = new Padding(16),
            ColumnCount = 2, RowCount = 0, AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(layout, "Current state", _state);
        Add(layout, "Manifest SHA-256", _manifest);
        Add(layout, "Evidence reference", _evidence);
        Add(layout, "Authorized reviewer", _reviewer);
        Add(layout, "Hold / revoke reason", _reason);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, BackColor = Color.Black,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true
        };
        actions.Controls.AddRange([_submit, _accept, _revoke, _hold, _release, _resynchronize]);
        Add(layout, "Durable actions", actions);
        Controls.Add(layout);

        _submit.Click += async (_, _) => await ExecuteAsync(() =>
            _viewModel.SubmitQualificationEvidenceAsync(_manifest.Text.Trim(), _evidence.Text.Trim()));
        _accept.Click += async (_, _) => await AcceptAsync();
        _revoke.Click += async (_, _) => await ExecuteAsync(() =>
            _viewModel.RevokeQualificationAsync(Required(_reason, "A revocation reason is required."),
                Required(_reviewer, "An authorized reviewer is required.")));
        _hold.Click += async (_, _) => await ExecuteAsync(() =>
            _viewModel.SetManualHoldAsync(true, Required(_reason, "A hold reason is required.")));
        _release.Click += async (_, _) => await ExecuteAsync(() =>
            _viewModel.SetManualHoldAsync(false, Required(_reason, "A release reason is required.")));
        _resynchronize.Click += async (_, _) => await ExecuteAsync(
            () => _viewModel.RequestResynchronizationAsync());
        Shown += async (_, _) => await RefreshAsync();
    }

    /// <summary>Reloads the durable account, evidence, qualification, and operational gate.</summary>
    public async Task RefreshAsync()
    {
        var result = await _viewModel.GetAccountAsync().ConfigureAwait(true);
        _current = result.Success ? result.Value : null;
        if (_current is null)
        {
            _state.Text = $"Unavailable ({result.ErrorCode}: {result.ErrorMessage})";
            SetButtonsEnabled(false);
            return;
        }

        _manifest.Text = _current.ManifestHash;
        _evidence.Text = _current.EvidenceReference;
        _state.Text = $"{_current.Id.AccountAlias} / {_current.Environment} | " +
            $"Qualification={_current.QualificationStatus} | Gate={_current.Gate} | " +
            $"Hold={_current.ManualHold} | Approval={Format(_current.ApprovalId)} | " +
            $"Revision={_current.Revision} | {_current.Reason}";
        SetButtonsEnabled(true);
    }

    private async Task AcceptAsync()
    {
        if (_current?.QualificationStatus != BrokerAccountQualificationStatus.ReviewPending)
            throw new InvalidOperationException("Submit qualification evidence before accepting it.");
        var manifest = Required(_manifest, "A manifest SHA-256 is required.");
        if (!string.Equals(manifest, _current.ManifestHash, StringComparison.Ordinal))
            throw new InvalidOperationException("The reviewed manifest must exactly match the submitted manifest.");
        var reviewer = Required(_reviewer, "An authorized reviewer is required.");
        var decision = MessageBox.Show(this,
            $"Accept emulator qualification for manifest {manifest}?\n\nReviewer: {reviewer}",
            "Accept Emulator Qualification", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (decision != DialogResult.Yes)
            return;
        await ExecuteAsync(() => _viewModel.AcceptQualificationAsync(Guid.NewGuid(), manifest, reviewer));
    }

    private async Task ExecuteAsync(Func<ValueTask<ServiceResult<Guid>>> action)
    {
        try
        {
            SetButtonsEnabled(false);
            var result = await action().ConfigureAwait(true);
            if (!result.Success)
                throw new InvalidOperationException($"Command failed ({result.ErrorCode}): {result.ErrorMessage}");
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _state.Text = exception.Message;
            SetButtonsEnabled(_current is not null);
        }
    }

    private void SetButtonsEnabled(bool accountAvailable)
    {
        _submit.Enabled = accountAvailable;
        _accept.Enabled = accountAvailable &&
            _current?.QualificationStatus == BrokerAccountQualificationStatus.ReviewPending;
        _revoke.Enabled = accountAvailable &&
            _current?.QualificationStatus == BrokerAccountQualificationStatus.Accepted;
        _hold.Enabled = accountAvailable && _current?.ManualHold == false;
        _release.Enabled = accountAvailable && _current?.ManualHold == true;
        _resynchronize.Enabled = accountAvailable;
    }

    private static string Required(TextBox input, string message) =>
        !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : throw new InvalidOperationException(message);

    private static string Format(Guid value) => value == Guid.Empty ? "None" : value.ToString("N");

    private static TextBox Input(string name, string value = "") => new()
    {
        Name = name, Text = value, Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Label ValueLabel(string text) => new()
    {
        AutoSize = true, MaximumSize = new Size(560, 0), Text = text,
        BackColor = Color.Black, ForeColor = Color.White, Padding = new Padding(4)
    };

    private static Button ActionButton(string name, string text) => new()
    {
        Name = name, Text = text, AutoSize = true,
        BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat, Margin = new Padding(4)
    };

    private static void Add(TableLayoutPanel panel, string label, Control value)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            AutoSize = true, Text = label, BackColor = Color.Black,
            ForeColor = Color.Silver, Padding = new Padding(4)
        }, 0, row);
        panel.Controls.Add(value, 1, row);
    }
}
