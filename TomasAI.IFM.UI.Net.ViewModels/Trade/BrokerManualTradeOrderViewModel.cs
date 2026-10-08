using Microsoft.Extensions.Logging;
﻿using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Creates broker-neutral manual Futures, Vertical Spread and Iron Condor opening candidates.</summary>
public sealed class BrokerManualTradeOrderViewModel
{
    const string EmulatorAccountAlias = "IFM-EMULATOR-PAPER";
    readonly IAppRoot _appRoot;
    readonly int _portfolioId;
    readonly PortfolioFundOrderEditorModel _fundOrder;
    readonly PortfolioFundOrderTradeEditorModel _trade;
    readonly FuturesContractV3ReadModel _baseContract;
    BrokerOrderType _brokerOrderType = BrokerOrderType.Limit;
    BrokerAlgorithm _brokerAlgorithm = BrokerAlgorithm.None;
    public IReadOnlyList<TradeOrderDefinition> SubmittedTradeOrders { get; private set; } = [];
    string _timeInForce = "Day";
    string _algorithmPace = "Normal";

    /// <summary>Creates an editor model for one existing Fund order trade composition.</summary>
    public BrokerManualTradeOrderViewModel(IAppRoot appRoot, int portfolioId,
        PortfolioFundOrderEditorModel fundOrder, PortfolioFundOrderTradeEditorModel trade,
        FuturesContractV3ReadModel baseContract)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _portfolioId = portfolioId;
        _fundOrder = fundOrder ?? throw new ArgumentNullException(nameof(fundOrder));
        _trade = trade ?? throw new ArgumentNullException(nameof(trade));
        _baseContract = baseContract ?? throw new ArgumentNullException(nameof(baseContract));
        if (trade.TradeType is not (TradeType.FuturesOutright or TradeType.PutCreditSpread or
            TradeType.PutDebitSpread or TradeType.CallCreditSpread or TradeType.CallDebitSpread or
            TradeType.ShortIronCondor or TradeType.LongIronCondor))
            throw new ArgumentException($"Trade type {trade.TradeType} is not supported by this editor.", nameof(trade));
    }

    /// <summary>Gets the strategy displayed by the editor.</summary>
    public TradeStrategyKind StrategyKind => _trade.TradeType == TradeType.FuturesOutright
        ? TradeStrategyKind.FuturesOutright
        : _trade.TradeType is TradeType.ShortIronCondor or TradeType.LongIronCondor
            ? TradeStrategyKind.IronCondor : TradeStrategyKind.VerticalSpread;

    /// <summary>Gets the selected trade composition.</summary>
    public PortfolioFundOrderTradeEditorModel Trade => _trade;

    /// <summary>Gets the selected Fund order.</summary>
    public PortfolioFundOrderEditorModel FundOrder => _fundOrder;

    /// <summary>Gets the exact synthetic broker account controlled by this editor.</summary>
    public BrokerAccountId BrokerAccountId => new(EmulatorAccountAlias);

    /// <summary>Gets the exact broker-neutral contract identifiers rendered in the editor.</summary>
    private (string ContractId, decimal Strike, bool IsCall)[]? _selectedOptionLegs;
    public DateOnly? SelectedOptionExpiry { get; private set; }

    public string[] ContractIds => _screenLegs?.Select(x => x.ContractId).ToArray() ?? (StrategyKind == TradeStrategyKind.FuturesOutright
        ? [_baseContract.ContractId]
        : _selectedOptionLegs?.Select(leg => leg.ContractId).ToArray() ?? _trade.GetContractIds());

    public void SetOptionLegSelection(DateOnly expiry, (string ContractId, decimal Strike, bool IsCall)[] legs)
    {
        if (expiry == default || StrategyKind != TradeStrategyKind.VerticalSpread || legs.Length != 2
            || legs.Any(leg => string.IsNullOrWhiteSpace(leg.ContractId) || leg.Strike <= 0)
            || legs[0].IsCall != legs[1].IsCall || legs[0].ContractId == legs[1].ContractId
            || legs[0].IsCall != _trade.TradeType.ToString().StartsWith("Call", StringComparison.Ordinal))
            throw new InvalidOperationException("Select two distinct contracts on the spread's call or put side.");
        _selectedOptionLegs = legs.OrderBy(leg => leg.Strike * (leg.IsCall ? 1 : -1)).ToArray();
        SelectedOptionExpiry = expiry;
    }

    TradeLegDefinition[]? _screenLegs;
    string? _selectedQuoteInputs;

    /// <summary>Captures the actual Broker Trade draft without depending on a hidden legacy editor.</summary>
    public void SetScreenLegSelection(DateOnly expiry, IReadOnlyList<BrokerTradeLegData> legs,
        IReadOnlyDictionary<string, int> quantities)
    {
        var required = StrategyKind == TradeStrategyKind.FuturesOutright ? 1
            : StrategyKind == TradeStrategyKind.IronCondor ? 4 : 2;
        if (expiry == default || legs.Count != required || legs.Select(x => x.ContractId).Distinct().Count() != required
            || legs.Any(x => string.IsNullOrWhiteSpace(x.ContractId) || x.Sign == 0
                || !quantities.TryGetValue(x.ContractId, out var q) || q < 1
                || x.IsFuture != (StrategyKind == TradeStrategyKind.FuturesOutright)
                || (!x.IsFuture && x.Strike is not > 0)))
            throw new InvalidOperationException("Select all contracts, directions and positive quantities before placing the order.");
        if (quantities.Where(x => legs.Any(l => l.ContractId == x.Key)).Select(x => x.Value).Distinct().Count() != 1)
            throw new InvalidOperationException("Spread leg quantities must be equal.");
        if (StrategyKind == TradeStrategyKind.IronCondor
            && (legs.Count(x => x.IsCall) != 2 || legs.Count(x => x.Sign > 0) != 2
                || legs.Where(x => x.IsCall).Sum(x => x.Sign) != 0
                || legs.Where(x => !x.IsCall).Sum(x => x.Sign) != 0))
            throw new InvalidOperationException("An iron condor requires one long and one short call and put.");
        if (StrategyKind == TradeStrategyKind.VerticalSpread
            && (legs.Select(x => x.IsCall).Distinct().Count() != 1 || legs.Sum(x => x.Sign) != 0
                || legs[0].IsCall != _trade.TradeType.ToString().StartsWith("Call", StringComparison.Ordinal)))
            throw new InvalidOperationException("A vertical spread requires one long and one short contract on the same side.");
        _selectedQuoteInputs = System.Text.Json.JsonSerializer.Serialize(legs);
        SelectedOptionExpiry = expiry;
        _screenLegs = legs.Select(x => NewLeg(x.ContractId, x.Sign * quantities[x.ContractId],
            x.IsFuture ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption,
            x.Strike ?? 0m, x.IsFuture ? (byte)0 : x.IsCall ? (byte)1 : (byte)2,
            x.Multiplier is > 0 ? x.Multiplier.Value : decimal.Parse(_baseContract.Multiplier, CultureInfo.InvariantCulture))).ToArray();
    }

    /// <summary>Applies the operator execution choices before the candidate is created.</summary>
    public void SetExecutionSelection(BrokerOrderType orderType, BrokerAlgorithm algorithm, string timeInForce = "Day", string algorithmPace = "Normal")
    {
        if (orderType is not (BrokerOrderType.Market or BrokerOrderType.Limit) ||
            algorithm is not (BrokerAlgorithm.None or BrokerAlgorithm.Adaptive))
            throw new InvalidOperationException($"Unsupported broker execution selection: {orderType} / {algorithm}.");
        _brokerOrderType = orderType;
        if (timeInForce is not ("Day" or "GTC") || algorithmPace is not ("Patient" or "Normal" or "Urgent"))
            throw new ArgumentException("Unsupported time in force or algorithm pace.");
        _brokerAlgorithm = algorithm;
        _timeInForce = timeInForce;
        _algorithmPace = algorithmPace;
    }

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
    public async Task RemoveAsync()
    {
        var orders = await _appRoot.Services.PortfolioQueries.GetOrdersAsync(_portfolioId, _trade.FundId,
            new DateOnly(_trade.RequestedTradeDate.Year, _trade.RequestedTradeDate.Month, 1), 200);
        var order = orders.Success && orders.Value is not null
            ? orders.Value.Items.SingleOrDefault(value => value.OrderId == _trade.OrderId)
            : null;
        if (order is null) throw new InvalidOperationException($"Portfolio order {_trade.OrderId} was not found.");
        var request = new ManualFundOrderTradeMutationRequest
        {
            PortfolioId = _portfolioId,
            FundId = _trade.FundId,
            OrderId = _trade.OrderId,
            ExpectedOrderVersion = order.AggregateVersion,
            TradeId = _trade.TradeId,
            RequestedAtUtc = DateTime.UtcNow
        };
        var result = await _appRoot.Services.PortfolioFundCommands.RemoveManualTradeAsync(request);
        if (!result.Success) throw new InvalidOperationException(result.ErrorMessage ?? "Unable to remove the Portfolio order trade.");
    }

    /// <summary>Confirms and submits one opening order through Portfolio and the Trade Order lifecycle.</summary>
    public async Task<Guid> SubmitAsync(int quantity, decimal signedNetDebitLimit,
        ITradeOrderConfirmationService confirmationService, CancellationToken cancellationToken = default)
    {
        var logger = _appRoot.DiagnosticLogger;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var commandId = await SubmitCoreAsync(quantity, signedNetDebitLimit, confirmationService, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("{Component}.{Method} completed; PortfolioId={PortfolioId}; FundId={FundId}; OrderId={OrderId}; TradeId={TradeId}; Quantity={Quantity}; OrderPrice={OrderPrice}; Strategy={Strategy}; CommandId={CommandId}; Outcome={Outcome}; ElapsedMilliseconds={ElapsedMilliseconds}",
                nameof(BrokerManualTradeOrderViewModel), nameof(SubmitAsync), _portfolioId, _trade.FundId,
                _trade.OrderId, _trade.TradeId, quantity, signedNetDebitLimit, StrategyKind, commandId,
                commandId == Guid.Empty ? "NotSubmitted" : "Submitted", System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return commandId;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Component}.{Method} failed; PortfolioId={PortfolioId}; TradeId={TradeId}; Quantity={Quantity}; OrderPrice={OrderPrice}",
                nameof(BrokerManualTradeOrderViewModel), nameof(SubmitAsync), _portfolioId, _trade.TradeId, quantity, signedNetDebitLimit);
            throw;
        }
    }

    async Task<Guid> SubmitCoreAsync(int quantity, decimal signedNetDebitLimit,
        ITradeOrderConfirmationService confirmationService, CancellationToken cancellationToken = default)
    {
        if (_portfolioId <= 0)
            throw new InvalidOperationException("Select a Portfolio before submitting the order.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order quantity must be positive.");
        if (_screenLegs is not null && _screenLegs.Any(x => Math.Abs(x.SignedQuantity) != quantity))
            throw new InvalidOperationException("Order quantity must match every selected leg quantity.");
        var cashMultiplier = _screenLegs?.FirstOrDefault()?.CashMultiplier
            ?? decimal.Parse(_baseContract.Multiplier, CultureInfo.InvariantCulture);
        var cashAmount = signedNetDebitLimit * quantity * cashMultiplier;
        var now = DateTime.UtcNow;
        var summary = new TradeOrderReadModel(
            _trade.FundId, _trade.OrderId, _trade.TradeId, _trade.RequestedTradeDate,
            _trade.TradeType, TradeSubType.Primary, _trade.RequestedTradeDate,
            SelectedOptionExpiry ?? _trade.RequestedMaturityDate ?? _trade.RequestedTradeDate,
            global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.TradeOrderState.OrderPlaced,
            _baseContract.ContractId, AssetType.Futures, string.Join(" / ", ContractIds),
            _trade.TradeAction == TradeAction.Sell ? OrderAction.Sell : OrderAction.Buy,
            OrderActionType.Open, quantity, 0, _brokerOrderType == BrokerOrderType.Market ? OrderType.Market : OrderType.Limit, signedNetDebitLimit,
            cashAmount, 0m, cashAmount, 0m,
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
        SubmittedTradeOrders = result.TradeOrders;
        var executedOrder = result.TradeOrders.Single(value => value.Id.FundId == _trade.FundId);
        var component = executedOrder.Components.Single();
        var setup = await _appRoot.Services.PortfolioQueries.GetOrderAsync(_trade.OrderId, cancellationToken);
        if (!setup.Success || setup.Value is null)
            throw new InvalidOperationException("The submitted execution could not be linked: setup order is unavailable.");
        var linked = await _appRoot.Services.PortfolioFundCommands.ChangeManualTradeStateAsync(new()
        {
            PortfolioId = _portfolioId, FundId = _trade.FundId, OrderId = _trade.OrderId,
            TradeId = _trade.TradeId, ExpectedOrderVersion = setup.Value.AggregateVersion,
            TradeState = TradeState.OrderSubmitted.ToString(), RequestedAtUtc = DateTime.UtcNow,
            ExecutionOrderId = executedOrder.Id.OrderId, ExecutionTradeId = component.ReservedTradeId
        }, cancellationToken);
        if (!linked.Success) throw new InvalidOperationException(
            "Order submitted, but its setup-to-execution link could not be persisted: " + linked.ErrorMessage);
        return result.PortfolioEventId;
    }

    async Task<PortfolioOrderCandidate> CreateCandidateAsync(int quantity, decimal limit,
        TradeFillType fillType, CancellationToken cancellationToken)
    {
        var contractIds = ContractIds;
        var requiredLegs = StrategyKind == TradeStrategyKind.FuturesOutright ? 1 : StrategyKind == TradeStrategyKind.IronCondor ? 4 : 2;
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
        var familyName = StrategyKind == TradeStrategyKind.FuturesOutright ? "Futures" : StrategyKind == TradeStrategyKind.IronCondor ? "IronCondor" : "VerticalSpread";
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
            StrategyKind, limit, quantity, _brokerOrderType, _brokerAlgorithm, _timeInForce, _algorithmPace,
            string.Join(';', legs.Select(leg => $"{leg.TradeLegId:N}:{leg.ContractId}:{leg.SignedQuantity}")));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))).ToLowerInvariant();
        var approvalReference = string.Empty;
        if (fillType == TradeFillType.Broker)
        {
            var account = await GetAccountAsync(cancellationToken).ConfigureAwait(false);
            if (!account.Success || account.Value is null)
                throw new InvalidOperationException(
                    $"The emulator account is unavailable ({account.ErrorCode}): {account.ErrorMessage}");
            if (!account.Value.DevelopmentQualificationsExempt && (account.Value.QualificationStatus != BrokerAccountQualificationStatus.Accepted ||
                account.Value.Gate != BrokerAccountOperationalGate.Open || account.Value.ApprovalId == Guid.Empty))
                throw new InvalidOperationException(
                    $"The emulator account cannot accept opening risk. " +
                    $"Qualification={account.Value.QualificationStatus}; Gate={account.Value.Gate}; " +
                    $"Reason={account.Value.Reason}");
            approvalReference = account.Value.DevelopmentQualificationsExempt ? "DevelopmentEmulatorQualificationExempt" : account.Value.ApprovalId.ToString("N");
        }

        var notional = Math.Abs(limit * quantity * multiplier);
        var maximumLoss = StrategyKind != TradeStrategyKind.FuturesOutright
            ? Math.Max(notional, SelectedOrderPriceRisk.MaximumOptionLoss(legs, limit, quantity)) : notional;
        return new PortfolioOrderCandidate
        {
            CompositionId = compositionId,
            WorkflowId = Guid.NewGuid(),
            DecisionHorizon = assignment.DecisionHorizon,
            StrategyKind = (PortfolioExecutionStrategyKind)StrategyKind,
            ValueDate = _trade.RequestedTradeDate,
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
                    MinimumSignedNetDebitLimit = limit - 10m * (StrategyKind == TradeStrategyKind.FuturesOutright ? 0.25m : 0.05m),
                    MaximumSignedNetDebitLimit = limit,
                    TickIncrement = StrategyKind == TradeStrategyKind.FuturesOutright ? 0.25m : 0.05m
                }.ToPortfolioComponent()
            ],
            RequiredCapital = maximumLoss,
            DecisionEvidence = TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence.CaptureJson("BrokerTradeScreen/v1", new
            {
                UnderlyingContract = _baseContract, TradeType = _trade.TradeType, SelectedLegs = legs,
                SelectedQuoteInputsJson = _selectedQuoteInputs, Quantity = quantity, OrderPrice = limit,
                RequiredCapital = maximumLoss, MaximumLoss = maximumLoss, Notional = notional,
                Deployment = assignment.TradeStrategyFamily!.CatalogDeployment, BrokerOrderType = _brokerOrderType,
                BrokerAlgorithm = _brokerAlgorithm, TimeInForce = _timeInForce, AlgorithmPace = _algorithmPace
            }, now),
            EvidenceHash = hash,
            DeploymentKey = assignment.TradeStrategyFamily!.CatalogDeployment!,
            MaximumLoss = maximumLoss,
            StressLoss = maximumLoss,
            Notional = notional,
            ProductSymbol = _baseContract.Symbol,
            ProductExchange = _baseContract.Exchange,
            ProductCurrency = _baseContract.Currency,
            PositionType = PortfolioExecutionPositionType.Opening,
            BrokerAccountAlias = EmulatorAccountAlias,
            BrokerEnvironment = PortfolioBrokerEnvironment.Emulator,
            MicroExecutionProfileId = "ManualPriceImprovement",
            MicroExecutionProfileVersion = 1,
            MicroExecutionProfileHash = hash,
            AccountPromotionApprovalReference = approvalReference,
            BrokerOrderType = (PortfolioBrokerOrderType)_brokerOrderType,
            BrokerAlgorithm = (PortfolioBrokerAlgorithm)_brokerAlgorithm,
            TimeInForce = _timeInForce,
            AlgorithmPace = _algorithmPace
        };
    }

    TradeLegDefinition[] CreateLegs(string[] contractIds, int quantity, decimal multiplier)
    {
        if (_screenLegs is not null) return _screenLegs;
        if (StrategyKind == TradeStrategyKind.IronCondor)
            throw new InvalidOperationException("Select the four Broker Trade legs before submitting an iron condor.");
        if (StrategyKind == TradeStrategyKind.FuturesOutright)
            return
            [
                NewLeg(contractIds[0], _trade.TradeAction == TradeAction.Sell ? -quantity : quantity,
                    TradeAssetFamily.Futures, 0m, 0, multiplier)
            ];

        var values = _selectedOptionLegs is { } selected
            ? (Strikes: selected.Select(leg => leg.Strike).ToArray(), PutCall: selected[0].IsCall ? (byte)1 : (byte)2)
            : ParseVerticalReference();
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
            Expiry = SelectedOptionExpiry ?? _trade.RequestedMaturityDate ?? _trade.RequestedTradeDate,
            Strike = family == TradeAssetFamily.Futures ? null : strike,
            PutCall = putCall,
            CashMultiplier = multiplier
        };

    (decimal[] Strikes, byte PutCall) ParseVerticalReference()
    {
        var value = _trade.InstructionReference.Trim().ToUpperInvariant();
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
