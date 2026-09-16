using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Creates broker-neutral manual Futures and Vertical Spread opening candidates.</summary>
public sealed class BrokerManualTradeOrderViewModel
{
    const string EmulatorAccountAlias = "IFM-EMULATOR-PAPER";
    readonly IAppRoot _appRoot;
    readonly int _portfolioId;
    readonly FundOrderReadModel _fundOrder;
    readonly FundOrderTradeReadModel _trade;
    readonly FuturesContractV3ReadModel _baseContract;

    /// <summary>Creates an editor model for one existing Fund order trade composition.</summary>
    public BrokerManualTradeOrderViewModel(IAppRoot appRoot, int portfolioId,
        FundOrderReadModel fundOrder, FundOrderTradeReadModel trade,
        FuturesContractV3ReadModel baseContract)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _portfolioId = portfolioId;
        _fundOrder = fundOrder ?? throw new ArgumentNullException(nameof(fundOrder));
        _trade = trade ?? throw new ArgumentNullException(nameof(trade));
        _baseContract = baseContract ?? throw new ArgumentNullException(nameof(baseContract));
        if (trade.TradeType is not (TradeType.FuturesOutright or TradeType.PutCreditSpread or
            TradeType.PutDebitSpread or TradeType.CallCreditSpread or TradeType.CallDebitSpread))
            throw new ArgumentException($"Trade type {trade.TradeType} is not supported by this editor.", nameof(trade));
    }

    /// <summary>Gets the strategy displayed by the editor.</summary>
    public TradeStrategyKind StrategyKind => _trade.TradeType == TradeType.FuturesOutright
        ? TradeStrategyKind.FuturesOutright
        : TradeStrategyKind.VerticalSpread;

    /// <summary>Gets the selected trade composition.</summary>
    public FundOrderTradeReadModel Trade => _trade;

    /// <summary>Gets the selected Fund order.</summary>
    public FundOrderReadModel FundOrder => _fundOrder;

    /// <summary>Gets the exact synthetic broker account controlled by this editor.</summary>
    public BrokerAccountId BrokerAccountId => new(EmulatorAccountAlias);

    /// <summary>Gets the exact broker-neutral contract identifiers rendered in the editor.</summary>
    public string[] ContractIds => StrategyKind == TradeStrategyKind.FuturesOutright
        ? [_baseContract.ContractId]
        : _trade.GetContractIds();

    /// <summary>Reads the durable emulator account gate for presentation.</summary>
    public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<BrokerAccountDefinition>> GetAccountAsync(
        CancellationToken cancellationToken = default) =>
        _appRoot.Services.BrokerAccounts.GetAsync(BrokerAccountId, cancellationToken);

    /// <summary>Submits a version-bound emulator qualification manifest for human review.</summary>
    public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<Guid>> SubmitQualificationEvidenceAsync(
        string manifestHash, string evidenceReference, CancellationToken cancellationToken = default) =>
        _appRoot.Services.BrokerAccountCommands.SubmitQualificationEvidenceAsync(BrokerAccountId,
            manifestHash, evidenceReference, DateTime.UtcNow, cancellationToken);

    /// <summary>Records the human reviewer's explicit acceptance of the submitted manifest.</summary>
    public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<Guid>> AcceptQualificationAsync(
        Guid approvalId, string manifestHash, string authorizedBy,
        CancellationToken cancellationToken = default) =>
        _appRoot.Services.BrokerAccountCommands.AcceptQualificationAsync(BrokerAccountId,
            approvalId, manifestHash, authorizedBy, DateTime.UtcNow, cancellationToken);

    /// <summary>Revokes the current emulator qualification.</summary>
    public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<Guid>> RevokeQualificationAsync(
        string reason, string authorizedBy, CancellationToken cancellationToken = default) =>
        _appRoot.Services.BrokerAccountCommands.RevokeQualificationAsync(BrokerAccountId,
            reason, authorizedBy, DateTime.UtcNow, cancellationToken);

    /// <summary>Sets or releases the operator new-risk hold.</summary>
    public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<Guid>> SetManualHoldAsync(
        bool enabled, string reason, CancellationToken cancellationToken = default) => enabled
        ? _appRoot.Services.BrokerAccountCommands.SetManualHoldAsync(BrokerAccountId,
            reason, DateTime.UtcNow, cancellationToken)
        : _appRoot.Services.BrokerAccountCommands.ReleaseManualHoldAsync(BrokerAccountId,
            reason, DateTime.UtcNow, cancellationToken);

    /// <summary>Requests an explicit emulator account resynchronization.</summary>
    public ValueTask<TomasAI.IFM.Shared.EventSourcing.ServiceResult<Guid>> RequestResynchronizationAsync(
        CancellationToken cancellationToken = default) =>
        _appRoot.Services.BrokerAccountCommands.RequestResynchronizationAsync(BrokerAccountId,
            DateTime.UtcNow, cancellationToken);

    /// <summary>Removes this unsubmitted composition from its Fund order.</summary>
    public Task RemoveAsync() => _appRoot.Services.FundCommands.ExecuteObservableAsync(
        async model => _ = await model.RemoveTradeFromFundOrderAsync(_trade.Id));

    /// <summary>Confirms and submits one opening order through Portfolio and the Trade Order lifecycle.</summary>
    public async Task<Guid> SubmitAsync(int quantity, decimal signedNetDebitLimit,
        ITradeOrderConfirmationService confirmationService, CancellationToken cancellationToken = default)
    {
        if (_portfolioId <= 0)
            throw new InvalidOperationException("Select a Portfolio before submitting the order.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order quantity must be positive.");
        var now = DateTime.UtcNow;
        var summary = new TradeOrderReadModel(
            _trade.FundId, _trade.OrderId, _trade.TradeId, _fundOrder.TradeDate,
            _trade.TradeType, TradeSubType.Primary, _trade.TradeDate, _trade.MaturityDate,
            global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.TradeOrderState.OrderPlaced,
            _baseContract.ContractId, AssetType.Futures, string.Join(" / ", ContractIds),
            _trade.TradeAction == TradeAction.Sell ? OrderAction.Sell : OrderAction.Buy,
            OrderActionType.Open, quantity, 0, OrderType.Limit, signedNetDebitLimit,
            signedNetDebitLimit * quantity, 0m, signedNetDebitLimit * quantity, 0m,
            TradeFillType.Broker, now, Environment.UserName, now, Environment.UserName);
        var confirmation = await confirmationService.ConfirmAsync(summary).ConfigureAwait(false);
        if (!confirmation.IsConfirmed)
            return Guid.Empty;

        var candidate = await CreateCandidateAsync(quantity, signedNetDebitLimit,
            confirmation.TradeFillType, cancellationToken).ConfigureAwait(false);
        var result = await _appRoot.Services.PortfolioTradeOrders.SubmitOpeningAsync(
            _portfolioId, candidate,
            confirmation.TradeFillType == TradeFillType.Broker
                ? ExecutionChannel.Broker
                : ExecutionChannel.Manual,
            cancellationToken).ConfigureAwait(false);
        return result.PortfolioEventId;
    }

    async Task<PortfolioOrderCandidate> CreateCandidateAsync(int quantity, decimal limit,
        TradeFillType fillType, CancellationToken cancellationToken)
    {
        var contractIds = ContractIds;
        var requiredLegs = StrategyKind == TradeStrategyKind.FuturesOutright ? 1 : 2;
        if (contractIds.Length != requiredLegs || contractIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                $"{StrategyKind} requires exactly {requiredLegs} broker-neutral contract identifier(s). " +
                "Use the reference format P4500:4490 or C4500:4510 for a Vertical Spread.");

        var fund = await _appRoot.Services.PortfolioQueries
            .GetFundAsync(_portfolioId, _trade.FundId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!fund.Success || fund.Value is null)
            throw new InvalidOperationException(
                $"Portfolio Fund {_portfolioId}.{_trade.FundId} is unavailable ({fund.ErrorCode}): {fund.ErrorMessage}");
        var assignments = await _appRoot.Services.PortfolioQueries.GetAssignmentsAsync(
            _portfolioId, _trade.FundId, fund.Value.FundMandateVersion, cancellationToken).ConfigureAwait(false);
        if (!assignments.Success || assignments.Value is null)
            throw new InvalidOperationException(
                $"Portfolio assignments are unavailable ({assignments.ErrorCode}): {assignments.ErrorMessage}");
        var familyName = StrategyKind == TradeStrategyKind.FuturesOutright ? "Futures" : "VerticalSpread";
        var now = DateTime.UtcNow;
        var assignment = assignments.Value
            .Where(value => value.IsEffectiveAt(now) && value.TradeStrategyFamily?.CatalogDeployment is not null
                && (value.UnderlyingUniverse.Contains(_baseContract.Symbol, StringComparer.OrdinalIgnoreCase)
                    || value.UnderlyingUniverse.Contains(_baseContract.ContractId, StringComparer.OrdinalIgnoreCase))
                && value.TradeFamily.Contains(familyName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(value => value.Priority)
            .ThenByDescending(value => value.AssignmentVersion)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No effective {familyName} deployment is assigned to Portfolio Fund " +
                $"{_portfolioId}.{_trade.FundId} for {_baseContract.Symbol}.");

        if (!decimal.TryParse(_baseContract.Multiplier, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var multiplier) || multiplier <= 0)
            throw new InvalidOperationException(
                $"A numeric cash multiplier is required for {_baseContract.ContractId}.");

        var legs = CreateLegs(contractIds, quantity, multiplier);
        var componentId = Guid.NewGuid();
        var compositionId = Guid.NewGuid();
        var evidence = string.Join('|', _portfolioId, _trade.FundId, compositionId, componentId,
            StrategyKind, limit, quantity,
            string.Join(';', legs.Select(leg => $"{leg.TradeLegId:N}:{leg.ContractId}:{leg.SignedQuantity}")));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))).ToLowerInvariant();
        var approvalReference = string.Empty;
        if (fillType == TradeFillType.Broker)
        {
            var account = await GetAccountAsync(cancellationToken).ConfigureAwait(false);
            if (!account.Success || account.Value is null)
                throw new InvalidOperationException(
                    $"The emulator account is unavailable ({account.ErrorCode}): {account.ErrorMessage}");
            if (account.Value.QualificationStatus != BrokerAccountQualificationStatus.Accepted ||
                account.Value.Gate != BrokerAccountOperationalGate.Open || account.Value.ApprovalId == Guid.Empty)
                throw new InvalidOperationException(
                    $"The emulator account cannot accept opening risk. " +
                    $"Qualification={account.Value.QualificationStatus}; Gate={account.Value.Gate}; " +
                    $"Reason={account.Value.Reason}");
            approvalReference = account.Value.ApprovalId.ToString("N");
        }

        var notional = Math.Abs(limit * quantity * multiplier);
        return new PortfolioOrderCandidate
        {
            CompositionId = compositionId,
            WorkflowId = Guid.NewGuid(),
            DecisionHorizon = assignment.DecisionHorizon,
            StrategyKind = StrategyKind,
            ValueDate = _fundOrder.TradeDate,
            ValidUntilUtc = now.AddMinutes(5),
            Origin = "DesktopTradeOrder",
            Components =
            [
                new TradeOrderComponentDefinition
                {
                    ComponentId = componentId,
                    StrategyKind = StrategyKind,
                    Legs = legs,
                    SignedNetDebitLimit = limit,
                    MinimumSignedNetDebitLimit = limit,
                    MaximumSignedNetDebitLimit = limit,
                    TickIncrement = StrategyKind == TradeStrategyKind.FuturesOutright ? 0.25m : 0.05m
                }
            ],
            RequiredCapital = notional,
            EvidenceHash = hash,
            DeploymentKey = assignment.TradeStrategyFamily!.CatalogDeployment!,
            MaximumLoss = notional,
            StressLoss = notional,
            Notional = notional,
            ProductSymbol = _baseContract.Symbol,
            ProductExchange = _baseContract.Exchange,
            ProductCurrency = _baseContract.Currency,
            PositionType = TradeOrderPositionType.Opening,
            BrokerAccountAlias = EmulatorAccountAlias,
            BrokerEnvironment = BrokerEnvironment.Emulator,
            MicroExecutionProfileId = "ManualExactLimit",
            MicroExecutionProfileVersion = 1,
            MicroExecutionProfileHash = hash,
            AccountPromotionApprovalReference = approvalReference
        };
    }

    TradeLegDefinition[] CreateLegs(string[] contractIds, int quantity, decimal multiplier)
    {
        if (StrategyKind == TradeStrategyKind.FuturesOutright)
            return
            [
                NewLeg(contractIds[0], _trade.TradeAction == TradeAction.Sell ? -quantity : quantity,
                    TradeAssetFamily.Futures, 0m, 0, multiplier)
            ];

        var values = ParseVerticalReference();
        var firstSign = _trade.TradeType switch
        {
            TradeType.PutCreditSpread or TradeType.CallCreditSpread => -1,
            _ => 1
        };
        return
        [
            NewLeg(contractIds[0], firstSign * quantity, TradeAssetFamily.FuturesOption,
                values.Strikes[0], values.PutCall, multiplier),
            NewLeg(contractIds[1], -firstSign * quantity, TradeAssetFamily.FuturesOption,
                values.Strikes[1], values.PutCall, multiplier)
        ];
    }

    TradeLegDefinition NewLeg(string contractId, int signedQuantity, TradeAssetFamily family,
        decimal strike, byte putCall, decimal multiplier) => new()
    {
        TradeLegId = Guid.NewGuid(),
        AssetFamily = family,
        SignedQuantity = signedQuantity,
        ContractId = contractId,
        ContractKey = contractId,
        Expiry = _trade.MaturityDate,
        Strike = strike,
        PutCall = putCall,
        CashMultiplier = multiplier
    };

    (decimal[] Strikes, byte PutCall) ParseVerticalReference()
    {
        var value = _trade.Reference.Trim().ToUpperInvariant();
        var strikes = value.Length > 1
            ? value[1..].Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        if (strikes.Length != 2 || !decimal.TryParse(strikes[0], NumberStyles.Number,
                CultureInfo.InvariantCulture, out var first) || !decimal.TryParse(strikes[1], NumberStyles.Number,
                CultureInfo.InvariantCulture, out var second))
            throw new InvalidOperationException("Vertical Spread reference must use P4500:4490 or C4500:4510 format.");
        return ([first, second], value[0] == 'C' ? (byte)1 : (byte)2);
    }
}
