using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Displays durable account, broker-order, execution, fill, and commission evidence.</summary>
public sealed class BrokerExecutionEvidenceControl : UserControl
{
    private const string EmulatorAccountAlias = "IFM-EMULATOR-PAPER";
    private readonly IAppRoot _appRoot;
    private readonly TradeOrderId _tradeOrderId;
    private readonly Label _content;

    /// <summary>Creates a read-only evidence panel for one exact Portfolio/Fund/Trade Order identity.</summary>
    public BrokerExecutionEvidenceControl(IAppRoot appRoot, TradeOrderId tradeOrderId)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _tradeOrderId = tradeOrderId;
        Name = "brokerExecutionEvidence";
        AccessibleName = "Broker account, order, fill, fee, and execution evidence";
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Microsoft Sans Serif", 10F);
        _content = new Label
        {
            Name = "brokerExecutionEvidenceText",
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = Font,
            Padding = new Padding(16),
            TextAlign = ContentAlignment.TopLeft,
            Text = "Loading durable broker evidence..."
        };
        Controls.Add(_content);
        _ = RefreshAsync();
    }

    /// <summary>Reloads the current durable account, broker-order, and execution evidence.</summary>
    public async Task RefreshAsync()
    {
        var lines = new List<string>();
        try
        {
            if (!_tradeOrderId.IsValid)
            {
                SetContent(["Broker evidence: unavailable until a Portfolio identity is selected"]);
                return;
            }
            var account = await _appRoot.Services.BrokerAccounts.GetAsync(
                new BrokerAccountId(EmulatorAccountAlias)).ConfigureAwait(true);
            lines.Add(account.Success && account.Value is { } accountState
                ? $"Account: {accountState.Id.AccountAlias}; Gate={accountState.Gate}; " +
                  $"Qualification={accountState.QualificationStatus}; Cash={accountState.Snapshot?.CashBalance:N2}; " +
                  $"Available={accountState.Snapshot?.AvailableFunds:N2}"
                : $"Account: unavailable ({account.ErrorCode}: {account.ErrorMessage})");

            var brokerOrders = await _appRoot.Services.BrokerOrders.ListAsync(_tradeOrderId)
                .ConfigureAwait(true);
            if (!brokerOrders.Success || brokerOrders.Value is not { Length: > 0 } orders)
            {
                lines.Add(brokerOrders.Success
                    ? "Broker orders: none submitted"
                    : $"Broker orders: unavailable ({brokerOrders.ErrorCode}: {brokerOrders.ErrorMessage})");
                SetContent(lines);
                return;
            }
            foreach (var order in orders)
            {
                lines.Add($"Broker order {order.Id.ComponentId:N}: {order.Status}; " +
                    $"Limit={order.CurrentSignedNetDebitLimit}; Revision={order.BrokerRevision}; " +
                    $"Dispatch={order.DispatchCategory} {order.DispatchDetail}".TrimEnd());
                if (order.LastObservation is { } observation)
                    lines.Add($"Latest broker fact: {observation.Kind}; Contract={observation.ContractId}; " +
                        $"Quantity={observation.SignedQuantity}; Price={observation.Price}; " +
                        $"Commission={observation.Commission}; At={observation.OccurredAtUtc:O}");
            }
            foreach (var attempt in orders.Select(order => order.Id.Execution.ExecutionAttemptId).Distinct())
            {
                var execution = await _appRoot.Services.OrderExecutions.GetAsync(_tradeOrderId, attempt)
                    .ConfigureAwait(true);
                if (!execution.Success || execution.Value is null)
                {
                    lines.Add($"Execution {attempt:N}: unavailable ({execution.ErrorCode}: {execution.ErrorMessage})");
                    continue;
                }
                lines.Add($"Execution {attempt:N}: {execution.Value.Status}; " +
                    $"Fills={execution.Value.Fills.Length}; " +
                    $"Commission={execution.Value.Fills.Sum(fill => fill.Commission):N2}");
                foreach (var fill in execution.Value.Fills)
                    lines.Add($"  {fill.ContractId}: Quantity={fill.SignedQuantity}; Price={fill.Price}; " +
                        $"Commission={fill.Commission}; External={fill.ExternalExecutionId}; At={fill.FilledAtUtc:O}");
            }
            SetContent(lines);
        }
        catch (Exception exception)
        {
            SetContent([.. lines, $"Broker evidence failed: {exception.Message}"]);
        }
    }

    private void SetContent(IEnumerable<string> lines)
    {
        if (!IsDisposed)
            _content.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
    }
}
