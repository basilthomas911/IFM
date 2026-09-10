using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;

/// <summary>
/// Creates one explicit paper-trading authority through the same commands and immutable configuration
/// lifecycle used by the trading runtime. Existing records are verified and never silently replaced.
/// </summary>
public sealed class DevelopmentTradingPortfolioProvisioner(
    DevelopmentTradingPortfolioOptions options,
    FinancialDevelopmentPolicy development,
    IConfigurationDbContext configuration,
    IMarketDataApi marketData,
    IPortfolioBusinessIdAllocator identities,
    IPortfolioCommandApi portfolios,
    IPortfolioFundCommandApi funds,
    IPortfolioFinancialPolicyCommandApi policies,
    IPortfolioQueryApi queries,
    IPortfolioFinancialApi financial,
    IntrinsicTimeStrategyWorkflowOptions workflow,
    ILogger<DevelopmentTradingPortfolioProvisioner> logger)
{
    const string Principal = "IFM Development startup";
    static readonly DateTime ManifestEpoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<DevelopmentTradingPortfolioReport> EnsureAsync(CancellationToken token = default)
    {
        options.Validate();
        if (!development.IsDevelopmentEnvironment)
            throw new InvalidOperationException("Development paper Portfolio provisioning requires the Development host environment.");
        if (!options.Enabled)
            throw new InvalidOperationException("Development paper Portfolio provisioning is disabled.");

        var now = PostgreSqlUtc(DateTime.UtcNow);
        var products = await ProductsAsync(token).ConfigureAwait(false);
        var catalog = BuildCatalog(products.Future, products.Option);
        var selection = TradeSelectionDefaultProfiles.EngineeringDefaults();
        var construction = DevelopmentTradingPortfolioDefaults.ConstructionPolicies();
        var risks = DevelopmentTradingPortfolioOptions.Horizons.Select(RiskParameterSet.Default).ToArray();
        foreach (var item in selection) await EnsureSelectionPolicyAsync(item, now, token).ConfigureAwait(false);
        foreach (var item in construction) await EnsurePipelinePolicyAsync(CatalogPipelineParameterKind.OrderComposition,
            item.ParameterSetId, item.Version, item.Hash(),
            () => configuration.InsertSelectionConstructionDraftAsync(item, "Development paper-trading construction constraints", Principal, token),
            StrategyParameterSetKind.OrderComposition, now, token).ConfigureAwait(false);
        foreach (var item in risks) await EnsurePipelinePolicyAsync(CatalogPipelineParameterKind.RiskManagement,
            item.ParameterSetId, item.Version, item.Hash(),
            () => configuration.InsertRiskManagementDraftAsync(item, "Development paper-trading Risk Manager policy", Principal, token),
            StrategyParameterSetKind.RiskManagement, now, token).ConfigureAwait(false);
        foreach (var definition in catalog.Definitions)
            await EnsureCatalogAsync(definition, now, token).ConfigureAwait(false);

        var existing = await FindPortfolioAsync(token).ConfigureAwait(false);
        var created = existing is null;
        var portfolioId = existing?.PortfolioId ?? (await identities.AllocatePortfolioIdAsync(token).ConfigureAwait(false)).Id;
        var portfolio = existing ?? await CreatePortfolioAsync(portfolioId, token).ConfigureAwait(false);
        VerifyPortfolioIdentity(portfolio);

        var policy = await EnsureFinancialPolicyAsync(portfolio, catalog.Deployments, token).ConfigureAwait(false);
        portfolio = await RequiredEventually(
            () => queries.GetPortfolioAsync(portfolioId, cancellationToken: token),
            "Read development Portfolio", token).ConfigureAwait(false);
        var fundMap = new Dictionary<TimeFrameType, int>();
        foreach (var horizon in DevelopmentTradingPortfolioOptions.Horizons)
        {
            var fund = await EnsureFundAsync(portfolio, policy, horizon, catalog.For(horizon), selection.Single(x => x.TargetHorizon == horizon),
                construction.Single(x => x.ParameterSetId == DevelopmentTradingPortfolioDefaults.ConstructionId(horizon)), token).ConfigureAwait(false);
            fundMap.Add(horizon, fund.FundId);
            portfolio = await RequiredEventually(
                () => queries.GetPortfolioAsync(portfolioId, cancellationToken: token),
                "Refresh development Portfolio", token).ConfigureAwait(false);
        }

        if (portfolio.OperatingState == PortfolioOperatingState.Draft)
        {
            var revision = await PortfolioRevisionAsync(portfolioId, token).ConfigureAwait(false);
            await RequireSuccess(portfolios.ChangePortfolioStateAsync(new(portfolioId), revision, PortfolioOperatingState.Active,
                "Development paper-trading authority is complete", token), "Activate development Portfolio").ConfigureAwait(false);
        }
        else if (portfolio.OperatingState != PortfolioOperatingState.Active)
            throw new InvalidOperationException($"Development Portfolio is {portfolio.OperatingState}; automatic provisioning will not override operator state.");

        foreach (var pair in fundMap)
        {
            var fund = await RequiredEventually(
                () => queries.GetFundAsync(portfolioId, pair.Value, cancellationToken: token),
                "Read development Fund", token).ConfigureAwait(false);
            if (fund.OperatingState == FundOperatingState.Draft)
            {
                var revision = await FundRevisionAsync(portfolioId, fund.FundId, token).ConfigureAwait(false);
                await RequireSuccess(funds.ChangeFundStateAsync(new(portfolioId, fund.FundId), revision, FundOperatingState.Active,
                    "Development paper-trading authority is complete", token), $"Activate {pair.Key} Fund").ConfigureAwait(false);
            }
            else if (fund.OperatingState != FundOperatingState.Active)
                throw new InvalidOperationException($"Development {pair.Key} Fund is {fund.OperatingState}; automatic provisioning will not override operator state.");
        }

        await EnsureFinancialBookAsync(portfolioId, fundMap.Values.ToArray(), token).ConfigureAwait(false);

        var activationReferences = new List<WorkflowActivationReference>();
        foreach (var pair in fundMap)
        {
            var selectionPolicy = selection.Single(x => x.TargetHorizon == pair.Key);
            var activation = new TradeSelectionActivation
            {
                SchemaVersion = 1,
                ParameterSetId = DevelopmentTradingPortfolioDefaults.ActivationId(now.Year, pair.Key),
                Version = 1,
                PortfolioId = portfolioId,
                FundId = pair.Value,
                InstrumentRoot = options.InstrumentRoot,
                TargetHorizon = pair.Key,
                SelectionPolicyReference = new()
                {
                    Kind = CatalogPipelineParameterKind.TradeSelection,
                    Id = selectionPolicy.ParameterSetId,
                    Version = selectionPolicy.Version,
                    PayloadSha256 = TradeSelectionPolicy.Hash(selectionPolicy)
                }
            };
            await EnsurePipelinePolicyAsync(CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow,
                activation.ParameterSetId, activation.Version, activation.Hash(),
                () => configuration.InsertTradeSelectionActivationDraftAsync(activation, "Development paper-trading workflow activation", Principal, token),
                StrategyParameterSetKind.IntrinsicTimeStrategyWorkflow, now, token).ConfigureAwait(false);
            _ = await configuration.ResolveTradeSelectionActivationAsync(activation.ParameterSetId, 1, activation.Hash(), now, token).ConfigureAwait(false);
            activationReferences.Add(new(pair.Key, activation.ParameterSetId, 1, activation.Hash()));
        }
        workflow.Activations = [.. activationReferences];

        foreach (var pair in fundMap)
            _ = await RequiredEventually(
                () => queries.ResolveForSelectionAsync(portfolioId, pair.Value, now.Year, pair.Key.ToString(), options.InstrumentRoot,
                    now, Guid.NewGuid(), 1, Guid.NewGuid(), token),
                $"Resolve {pair.Key} selection authority", token).ConfigureAwait(false);

        logger.LogInformation("Development paper Portfolio {PortfolioId} is ready with {FundCount} Funds, {DeploymentCount} published deployments and {Capital} {Currency} separately posted capital",
            portfolioId, fundMap.Count, catalog.Deployments.Length, options.DevelopmentCapital, options.Currency);
        return new(portfolioId, policy.PolicyId, fundMap, options.DevelopmentCapital, catalog.Deployments.Length, created, true);
    }

    async Task<(CatalogProduct Future, CatalogProduct Option)> ProductsAsync(CancellationToken token)
    {
        var futures = await marketData.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, token).ConfigureAwait(false);
        var optionsResult = await marketData.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, token).ConfigureAwait(false);
        if (!futures.Success || futures.Value is null) throw new InvalidOperationException("Futures product catalog: " + futures.ErrorMessage);
        if (!optionsResult.Success || optionsResult.Value is null) throw new InvalidOperationException("Futures-option product catalog: " + optionsResult.ErrorMessage);
        var future = futures.Value.Where(x => x.Symbol == options.InstrumentRoot && x.Currency == options.Currency).ToArray();
        var option = optionsResult.Value.Where(x => x.Symbol == options.InstrumentRoot && x.Currency == options.Currency).ToArray();
        if (future.Length != 1 || option.Length != 1)
            throw new InvalidOperationException("Development provisioning requires one exact ES/USD Futures product and one exact ES/USD FuturesOption product. Refresh instrument definitions first.");
        return (new(future[0].Id, future[0].Symbol, future[0].Exchange, future[0].Currency),
            new(option[0].Id, option[0].Symbol, option[0].Exchange, option[0].Currency));
    }

    async Task<PortfolioReadModel?> FindPortfolioAsync(CancellationToken token)
    {
        var matches = new List<PortfolioReadModel>();
        foreach (var state in Enum.GetValues<PortfolioOperatingState>().Where(x => x != PortfolioOperatingState.Unknown))
        {
            string? page = null;
            do
            {
                var result = await queries.GetPortfoliosAsync(state, 200, page, token).ConfigureAwait(false);
                if (!result.Success || result.Value is null) throw new InvalidOperationException("Development Portfolio discovery failed: " + result.ErrorMessage);
                matches.AddRange(result.Value.Items.Where(x => x.Name == options.PortfolioName));
                page = result.Value.NextPageToken;
            } while (page is not null);
        }
        var identities = matches.GroupBy(x => x.PortfolioId).ToArray();
        return identities.Length switch
        {
            0 => null,
            1 => identities[0]
                .OrderByDescending(x => x.OperatingState == PortfolioOperatingState.Active)
                .ThenByDescending(x => x.PortfolioVersion)
                .First(),
            _ => throw new InvalidOperationException("Multiple Portfolios use the reserved Development Portfolio name.")
        };
    }

    async Task<PortfolioReadModel> CreatePortfolioAsync(int id, CancellationToken token)
    {
        var value = new PortfolioReadModel
        {
            PortfolioId = id, PortfolioVersion = 1, SchemaVersion = 3, Name = options.PortfolioName,
            BaseCurrency = options.Currency, OperatingState = PortfolioOperatingState.Draft,
            EffectiveFromUtc = ManifestEpoch, BrokerAccountRefs = [options.ExecutionAccountReference],
            CreatedOnUtc = ManifestEpoch, CreatedBy = Principal
        };
        await RequireSuccess(portfolios.CreatePortfolioAsync(value, StableId("portfolio"), token), "Create development Portfolio").ConfigureAwait(false);
        return await RequiredEventually(
            () => queries.GetPortfolioAsync(id, cancellationToken: token),
            "Read created development Portfolio", token).ConfigureAwait(false);
    }

    void VerifyPortfolioIdentity(PortfolioReadModel portfolio)
    {
        if (portfolio.Name != options.PortfolioName || portfolio.BaseCurrency != options.Currency
            || !portfolio.BrokerAccountRefs.Contains(options.ExecutionAccountReference, StringComparer.Ordinal))
            throw new InvalidOperationException("The reserved Development Portfolio identity conflicts with existing content.");
    }

    async Task<PortfolioFinancialPolicyReadModel> EnsureFinancialPolicyAsync(PortfolioReadModel portfolio, CatalogKey[] deployments, CancellationToken token)
    {
        var existing = await queries.GetPoliciesAsync(portfolio.PortfolioId, 200, token).ConfigureAwait(false);
        if (!existing.Success || existing.Value is null) throw new InvalidOperationException("Development policy discovery failed: " + existing.ErrorMessage);
        var matches = existing.Value.Items.Where(x => x.Name == "IFM Development Paper Limits").ToArray();
        if (matches.Length > 1) throw new InvalidOperationException("Multiple financial policies use the reserved Development policy name.");
        PortfolioFinancialPolicyReadModel policy;
        if (matches.Length == 0)
        {
            var policyId = await identities.AllocatePolicyIdAsync(token).ConfigureAwait(false);
            policy = Policy(portfolio.PortfolioId, policyId, deployments);
            await RequireSuccess(policies.CreatePolicyAsync(policy, StableId("financial-policy"), token), "Create Development financial policy").ConfigureAwait(false);
        }
        else
        {
            policy = matches[0];
            var expected = Policy(portfolio.PortfolioId, policy.PolicyId, deployments) with
            { OperatingState = policy.OperatingState, AggregateRevision = policy.AggregateRevision };
            if (policy.CanonicalSha256() != expected.CanonicalSha256())
                throw new InvalidOperationException("Existing Development financial policy differs from the configured manifest.");
        }
        if (portfolio.ActivePolicyId == 0)
        {
            var policyRevision = (await RequiredEventually(
                () => queries.GetPolicyAsync(policy.PolicyId, cancellationToken: token),
                "Read Development policy revision", token).ConfigureAwait(false)).AggregateRevision;
            var portfolioRevision = await PortfolioRevisionAsync(portfolio.PortfolioId, token).ConfigureAwait(false);
            await RequireSuccess(policies.ActivateAndAssignAsync(new(portfolio.PortfolioId, policy.PolicyId), policy.PolicyVersion,
                policyRevision, portfolioRevision, token), "Activate Development financial policy").ConfigureAwait(false);
            policy = await RequiredEventually(
                () => queries.GetActivePolicyAsync(portfolio.PortfolioId, token),
                "Read active Development policy", token).ConfigureAwait(false);
        }
        else if (portfolio.ActivePolicyId != policy.PolicyId || portfolio.ActivePolicyVersion != policy.PolicyVersion)
            throw new InvalidOperationException("Development Portfolio already references a different financial policy.");
        return policy;
    }

    PortfolioFinancialPolicyReadModel Policy(int portfolioId, int policyId, CatalogKey[] deployments) => new()
    {
        PortfolioId = portfolioId, PolicyId = policyId, PolicyVersion = 1, SchemaVersion = 3,
        Name = "IFM Development Paper Limits", OperatingState = PortfolioFinancialPolicyState.Draft,
        BaseCurrency = options.Currency, CapitalBase = options.DevelopmentCapital, ProtectedReserve = options.ProtectedReserve,
        MaximumDeployableCapital = options.DevelopmentCapital - options.ProtectedReserve,
        MaximumRiskPerTrade = options.MaximumRiskPerTrade, MaximumAggregateRisk = options.MaximumAggregateRisk,
        MaximumMargin = options.MaximumMargin, MaximumGrossNotional = options.MaximumGrossNotional,
        MaximumOpenPositions = options.MaximumOpenPositions, MaximumDrawdownAmount = options.MaximumDrawdown,
        TradeFamilyLimits = deployments.Select(x => new TradeFamilyRiskLimitReadModel
        {
            CatalogDeployment = x, Enabled = true, MaximumRiskPerTrade = options.MaximumRiskPerTrade,
            MaximumAggregateRisk = options.MaximumAggregateRisk, MaximumMargin = options.MaximumMargin,
            MaximumGrossNotional = options.MaximumGrossNotional, MaximumOpenPositions = options.MaximumOpenPositions
        }).ToArray(), EffectiveFromUtc = ManifestEpoch, CreatedOnUtc = ManifestEpoch, CreatedBy = Principal
    };

    async Task<FundMandateReadModel> EnsureFundAsync(PortfolioReadModel portfolio, PortfolioFinancialPolicyReadModel policy,
        TimeFrameType horizon, CatalogKey[] deployments, TradeSelectionParameterSet selection, SelectionConstructionPolicy construction,
        CancellationToken token)
    {
        var year = DateTime.UtcNow.Year;
        var code = $"DEV-{year}-{horizon.ToString().ToUpperInvariant()}";
        var page = await queries.GetFundsAsync(portfolio.PortfolioId, null, 200, cancellationToken: token).ConfigureAwait(false);
        if (!page.Success || page.Value is null) throw new InvalidOperationException("Development Fund discovery failed: " + page.ErrorMessage);
        var matches = page.Value.Items.Where(x => x.FundCode == code).ToArray();
        if (matches.Length > 1) throw new InvalidOperationException($"Multiple Funds use reserved code {code}.");
        FundMandateReadModel fund;
        var portfolioRevision = await PortfolioRevisionAsync(portfolio.PortfolioId, token).ConfigureAwait(false);
        if (matches.Length == 0)
        {
            var fundId = await identities.AllocateFundIdAsync(token).ConfigureAwait(false);
            fund = Fund(portfolio.PortfolioId, fundId, code, horizon, deployments);
            await RequireSuccess(funds.CreateFundMandateAsync(fund, StableId("fund/" + code), token), $"Create {horizon} Development Fund").ConfigureAwait(false);
            await RequireSuccess(portfolios.AddFundAsync(new(portfolio.PortfolioId, fundId), portfolioRevision, token), $"Add {horizon} Fund to Portfolio").ConfigureAwait(false);
            portfolioRevision++;
        }
        else fund = matches[0];

        if (!fund.PermittedTradeStrategyFamilies.Select(x => x.CatalogDeployment).SequenceEqual(deployments.Cast<CatalogKey?>()))
            throw new InvalidOperationException($"Existing {horizon} Development Fund deployment permissions differ from the manifest.");

        var assignments = await queries.GetAssignmentsAsync(portfolio.PortfolioId, fund.FundId, fund.FundMandateVersion, token).ConfigureAwait(false);
        if (!assignments.Success || assignments.Value is null) throw new InvalidOperationException("Development assignment discovery failed: " + assignments.ErrorMessage);
        var fundRevision = await FundRevisionAsync(portfolio.PortfolioId, fund.FundId, token).ConfigureAwait(false);
        foreach (var deployment in deployments)
        {
            if (assignments.Value.Any(x => x.TradeStrategyFamily?.CatalogDeployment == deployment)) continue;
            var stored = await configuration.GetStrategyCatalogAsync(deployment, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Development deployment disappeared during provisioning.");
            var asset = stored.Definition.Code.Contains("Future", StringComparison.Ordinal) ? "Futures" : "FuturesOption";
            var assignment = new FundTradeTemplateAssignmentReadModel
            {
                PortfolioId = portfolio.PortfolioId, PortfolioVersion = portfolio.PortfolioVersion,
                FundId = fund.FundId, FundMandateVersion = fund.FundMandateVersion, AssignmentVersion = fundRevision + 1,
                TradeTemplateId = deployment.Id, TradeTemplateVersion = deployment.Version, Enabled = true,
                DecisionHorizon = horizon.ToString(), UnderlyingUniverse = [options.InstrumentRoot], AssetType = asset,
                TradeFamily = stored.Definition.Code, Priority = stored.Definition.Code.Contains("Future", StringComparison.Ordinal) ? 10 : stored.Definition.Code.Contains("Vertical", StringComparison.Ordinal) ? 20 : 30,
                EffectiveFromUtc = ManifestEpoch, TradeSelectionHintProfileId = selection.ParameterSetId,
                TradeSelectionHintProfileVersion = selection.Version, OrderCompositionProfileId = construction.ParameterSetId,
                OrderCompositionProfileVersion = construction.Version, CreatedOnUtc = ManifestEpoch, CreatedBy = Principal,
                SchemaVersion = 3, TradeStrategyFamily = new(0, 0) { CatalogDeployment = deployment }
            };
            await RequireSuccess(funds.AssignTradeTemplateAsync(assignment, fundRevision, token), $"Assign {stored.Definition.Code}").ConfigureAwait(false);
            fundRevision++;
        }

        if (await queries.GetFundAllocationAsync(portfolio.PortfolioId, fund.FundId, token).ConfigureAwait(false) is { Success: false })
        {
            await RequireSuccess(portfolios.DelegateAllocationAsync(new()
            {
                PortfolioId = portfolio.PortfolioId, PortfolioVersion = portfolio.PortfolioVersion, FundId = fund.FundId,
                FundMandateVersion = fund.FundMandateVersion, AllocationVersion = 1, TargetWeight = 1m / 3m,
                MinimumWeight = 0, MaximumWeight = 1, AllocatedCapital = FundCapital(horizon),
                Currency = options.Currency, SourcePolicyId = policy.PolicyId, SourcePolicyVersion = policy.PolicyVersion,
                EffectiveFromUtc = ManifestEpoch, CreatedOnUtc = ManifestEpoch, CreatedBy = Principal
            }, portfolioRevision, token), $"Delegate {horizon} allocation").ConfigureAwait(false);
            portfolioRevision++;
        }
        var envelopeRead = await queries.GetFundRiskEnvelopeAsync(portfolio.PortfolioId, fund.FundId, DateTime.UtcNow, token).ConfigureAwait(false);
        if (!envelopeRead.Success)
        {
            var allocated = FundCapital(horizon);
            await RequireSuccess(portfolios.DelegateRiskEnvelopeAsync(new()
            {
                PortfolioId = portfolio.PortfolioId, PortfolioVersion = portfolio.PortfolioVersion, FundId = fund.FundId,
                FundMandateVersion = fund.FundMandateVersion, EnvelopeId = StableId("envelope/" + code), EnvelopeVersion = 1,
                CapacityState = FundCapacityState.Available, Currency = options.Currency, AllocatedCapital = allocated,
                AvailableCapital = allocated, MaximumRiskPerTrade = Math.Min(options.MaximumRiskPerTrade, allocated),
                MaximumAggregateRisk = Math.Min(options.MaximumAggregateRisk, allocated), MaximumMargin = Math.Min(options.MaximumMargin, allocated),
                MaximumGrossNotional = options.MaximumGrossNotional / 3m, MaximumContracts = options.MaximumContracts,
                MaximumOpenPositions = options.MaximumOpenPositions, MaximumDrawdown = Math.Min(options.MaximumDrawdown, allocated),
                RemainingLossBudget = Math.Min(options.MaximumDrawdown, allocated), EffectiveFromUtc = ManifestEpoch,
                ExpiresAtUtc = new(DateTime.UtcNow.Year + 2, 1, 1, 0, 0, 0, DateTimeKind.Utc), SourcePolicyId = policy.PolicyId,
                SourcePolicyVersion = policy.PolicyVersion, CreatedOnUtc = ManifestEpoch, CreatedBy = Principal
            }, portfolioRevision, token), $"Delegate {horizon} risk envelope").ConfigureAwait(false);
            portfolioRevision++;
        }
        _ = await PortfolioRevisionAsync(portfolio.PortfolioId, token, portfolioRevision).ConfigureAwait(false);
        _ = await FundRevisionAsync(portfolio.PortfolioId, fund.FundId, token, fundRevision).ConfigureAwait(false);
        return await RequiredEventually(
            () => queries.GetFundAsync(portfolio.PortfolioId, fund.FundId, cancellationToken: token),
            $"Read {horizon} Development Fund", token).ConfigureAwait(false);
    }

    FundMandateReadModel Fund(int portfolioId, int fundId, string code, TimeFrameType horizon, CatalogKey[] deployments) => new()
    {
        PortfolioId = portfolioId, FundId = fundId, FundCode = code, Name = $"ES {horizon} Development Fund",
        FundMandateVersion = 1, SchemaVersion = 3, TradingYear = DateTime.UtcNow.Year,
        OperatingState = FundOperatingState.Draft, EffectiveFromUtc = new(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EffectiveUntilUtc = new(DateTime.UtcNow.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), DecisionHorizon = horizon.ToString(),
        Objective = "Exercise the production decision, composition, capacity and risk path with paper capital.",
        UnderlyingUniverse = [options.InstrumentRoot], EligibleAssetTypes = ["Futures", "FuturesOption"],
        PermittedDirections = Enum.GetNames<MarketConditionDirection>().Where(x => x != nameof(MarketConditionDirection.Undefined)).ToArray(),
        PermittedConditions = Enum.GetNames<MarketConditionType>().Where(x => x != nameof(MarketConditionType.Undefined)).ToArray(),
        PermittedTradeFamilies = deployments.Select(DevelopmentDeploymentCode).ToArray(),
        PermittedTradeStrategyFamilies = deployments.Select(x => new TradeStrategyFamilyReference(0, 0) { CatalogDeployment = x }).ToArray(),
        CreatedOnUtc = ManifestEpoch, CreatedBy = Principal
    };

    async Task EnsureFinancialBookAsync(int portfolioId, int[] fundIds, CancellationToken token)
    {
        var scope = new FinancialReadScope { PortfolioId = portfolioId, Access = new(Principal, ["PortfolioAdministrator"]) };
        var existing = await financial.GetFinancialLedgerConfigurationAsync(scope, new(), token).ConfigureAwait(false);
        if (existing.Success && existing.Value?.Status == FinancialReadStatus.Found)
        {
            var value = existing.Value.Value!;
            if (value.Environment != "Emulator" || value.OperatingState != "Active")
                throw new InvalidOperationException($"Development ledger exists but is {value.Environment}/{value.OperatingState}; operator recovery is required.");
            var balance = await financial.GetAccountBalancesAsync(scope, new(), token).ConfigureAwait(false);
            if (!balance.Success || balance.Value?.Value is not { MigrationQualified: true })
                throw new InvalidOperationException("Development ledger is missing qualified, separately posted capital.");
            for (var index = 0; index < fundIds.Length; index++)
            {
                var posting = await financial.GetPostingReceiptAsync(
                    scope, new(StableId("development-capital/" + index)), token).ConfigureAwait(false);
                if (!posting.Success || posting.Value is not { Status: FinancialReadStatus.Found, Value.Posting.Receipt.FundId: var fundId }
                    || fundId != fundIds[index])
                    throw new InvalidOperationException($"Development Fund {fundIds[index]} is missing its separately entered capital receipt.");
            }
            return;
        }

        var prepared = await financial.PrepareFinancialBookAsync(scope,
            new(options.ExecutionAccountReference, new(2026, 1, 1), new(2099, 12, 31)), token).ConfigureAwait(false);
        var draft = prepared.Value?.Value?.Draft ?? throw new InvalidOperationException("Development ledger preparation failed: " + prepared.ErrorMessage);
        await ConfigureAsync(portfolioId, 0, draft with { Reason = "Create Development paper-trading book" }, token).ConfigureAwait(false);
        var rule = draft.Rules.Single(x => x.Kind == LedgerTransactionKind.DepositConfirmed);
        var requested = ManifestEpoch;
        for (var index = 0; index < fundIds.Length; index++)
        {
            var operation = StableId("development-capital/" + index);
            var amount = index == fundIds.Length - 1
                ? options.DevelopmentCapital - Enumerable.Range(0, index).Sum(FundCapitalByIndex)
                : FundCapitalByIndex(index);
            var post = new PostFundTransactionCommand
            {
                CommandId = operation, OperationId = operation, PortfolioId = portfolioId, EntityId = new(portfolioId),
                Subject = new(ActorType.Command, PostFundTransactionCommand.Actor, PostFundTransactionCommand.Verb, portfolioId.ToString()),
                CorrelationId = operation, CausationId = operation, RequestedAtUtc = requested, ExpiresAtUtc = new(2099, 12, 31),
                ExpectedFinancialRevision = index + 1, Access = new(Principal, ["PortfolioAdministrator"]),
                Body = new()
                {
                    BookId = draft.BookId, FundId = fundIds[index], Amount = amount, Currency = options.Currency,
                    TransactionKind = LedgerTransactionKind.DepositConfirmed, AccountingDate = DateOnly.FromDateTime(ManifestEpoch),
                    ValueDate = DateOnly.FromDateTime(ManifestEpoch), Description = "Separately entered Development paper capital",
                    PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash },
                    Source = new() { System = "DevelopmentPortfolioProvisioner", SourceEntityId = options.PortfolioName,
                        SourceEventId = operation, SourceContentHash = new('D', 64), OccurredAtUtc = requested },
                    MovementEvidence = new() { Status = MovementStatus.Confirmed, SourceReference = "Configured paper capital; no live cash movement" }
                }
            };
            post = post with { InputSha256 = FinancialCanonicalHash.Request(post) };
            await RequireFinancial(await financial.PostAsync(post, token).ConfigureAwait(false), "Post Development capital").ConfigureAwait(false);
        }

        var reconcileId = StableId("development-reconciliation");
        var capitalCut = $"development-capital:{StableId("development-capital/0")}:{StableId("development-capital/1")}:{StableId("development-capital/2")}";
        await ConfigureAsync(portfolioId, fundIds.Length + 1, new()
        {
            Action = LedgerConfigurationAction.Reconcile, BookId = draft.BookId, SourceCut = capitalCut,
            Reason = "Reconcile separately entered Development capital"
        }, token, reconcileId).ConfigureAwait(false);
        await ConfigureAsync(portfolioId, fundIds.Length + 2, new()
        {
            Action = LedgerConfigurationAction.QualifyDevelopmentBook, BookId = draft.BookId, Book = draft.Book,
            ReconciliationId = reconcileId, SourceCut = capitalCut, Reason = "Qualify fresh Development paper book"
        }, token, StableId("development-qualification")).ConfigureAwait(false);

        var authority = await financial.PrepareFinancialAuthorityAsync(scope, new(true), token).ConfigureAwait(false);
        var authorityDraft = authority.Value?.Value?.Draft ?? throw new InvalidOperationException("Development financial authority preparation failed: " + authority.ErrorMessage);
        await ConfigureAsync(portfolioId, authority.Value!.FinancialRevision, authorityDraft with { Reason = "Enable current Development Fund spending" }, token,
            StableId("development-authority")).ConfigureAwait(false);
        ServiceResult<FinancialRead<FinancialLedgerConfiguration>>? verified = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        do
        {
            verified = await financial.GetFinancialLedgerConfigurationAsync(scope, new(), token).ConfigureAwait(false);
            if (verified.Success && verified.Value is { Status: FinancialReadStatus.Found, Value.OperatingState: "Active" }) return;
            await Task.Delay(50, token).ConfigureAwait(false);
        } while (DateTime.UtcNow < deadline);
        throw new InvalidOperationException(
            $"Development financial book did not reach Active (read success {verified?.Success}, " +
            $"status {verified?.Value?.Status}, state {verified?.Value?.Value?.OperatingState}, error {verified?.ErrorMessage}).");
    }

    async Task ConfigureAsync(int portfolioId, long revision, LedgerConfigurationRequest body, CancellationToken token, Guid? id = null)
    {
        var operation = id ?? StableId("ledger-create");
        var command = new ConfigureLedgerCommand
        {
            CommandId = operation, OperationId = operation, PortfolioId = portfolioId, EntityId = new(portfolioId),
            Subject = new(ActorType.Command, ConfigureLedgerCommand.Actor, ConfigureLedgerCommand.Verb, portfolioId.ToString()),
            CorrelationId = operation, CausationId = operation, RequestedAtUtc = ManifestEpoch, ExpiresAtUtc = new(2099, 12, 31),
            ExpectedFinancialRevision = revision, Access = new(Principal, ["PortfolioAdministrator", "LedgerConfigure", "LedgerImport"]), Body = body
        };
        command = command with { InputSha256 = FinancialCanonicalHash.Request(command) };
        await RequireFinancial(await financial.ConfigureAsync(command, token).ConfigureAwait(false), body.Action.ToString()).ConfigureAwait(false);
    }

    async Task EnsureSelectionPolicyAsync(TradeSelectionParameterSet policy, DateTime now, CancellationToken token)
    {
        var current = await configuration.GetTradeSelectionVersionAsync(policy.ParameterSetId, policy.Version, token).ConfigureAwait(false);
        var hash = TradeSelectionPolicy.Hash(policy);
        if (current is null) await configuration.InsertTradeSelectionDraftAsync(policy, "Development paper-trading selector policy", Principal, token).ConfigureAwait(false);
        else if (current.PayloadSha256 != hash) throw new InvalidOperationException($"Development selector {policy.TargetHorizon} hash conflict.");
        if (current?.Status != ConfigurationParameterSetStatus.Published)
            await configuration.PublishAsync(StrategyParameterSetKind.TradeSelection, policy.ParameterSetId, policy.Version, now.AddSeconds(-1), token).ConfigureAwait(false);
        _ = await configuration.ResolveTradeSelectionVersionAsync(policy.ParameterSetId, policy.Version, hash, now, token).ConfigureAwait(false);
    }

    async Task EnsurePipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, string hash,
        Func<Task> insert, StrategyParameterSetKind publishKind, DateTime now, CancellationToken token)
    {
        var current = await configuration.GetSelectionPipelinePolicyAsync(kind, id, version, token).ConfigureAwait(false);
        if (current is null) await insert().ConfigureAwait(false);
        else if (current.PayloadSha256 != hash)
            throw new InvalidOperationException(
                $"Development {kind} {id}/v{version} conflicts with the immutable manifest " +
                $"(stored {current.PayloadSha256}, expected {hash}).");
        if (current?.Status != CatalogLifecycleStatus.Published)
            await configuration.PublishAsync(publishKind, id, version, now.AddSeconds(-1), token).ConfigureAwait(false);
        var verified = await configuration.GetSelectionPipelinePolicyAsync(kind, id, version, token).ConfigureAwait(false);
        if (verified is not { Status: CatalogLifecycleStatus.Published } || verified.PayloadSha256 != hash)
            throw new InvalidOperationException($"Development {kind} failed publication verification.");
    }

    async Task EnsureCatalogAsync(StrategyCatalogDefinition definition, DateTime now, CancellationToken token)
    {
        var expected = StrategyCatalogValidation.ContentHash(definition);
        var current = await configuration.GetStrategyCatalogAsync(definition.Key, token).ConfigureAwait(false);
        if (current is null)
        {
            var inserted = await configuration.InsertStrategyCatalogDraftAsync(definition, 0, Principal, token).ConfigureAwait(false);
            if (inserted != expected) throw new InvalidOperationException("Development catalog insert returned an unexpected hash.");
            current = await configuration.GetStrategyCatalogAsync(definition.Key, token).ConfigureAwait(false);
        }
        if (current?.ContentHash != expected) throw new InvalidOperationException($"Development catalog {definition.Code} conflicts with existing immutable content.");
        if (current.Status == CatalogLifecycleStatus.Draft)
            await configuration.PublishStrategyCatalogAsync(definition.Key, expected, now.AddSeconds(-1), Principal, token).ConfigureAwait(false);
        else if (current.Status != CatalogLifecycleStatus.Published)
            throw new InvalidOperationException($"Development catalog {definition.Code} is retired.");
    }

    async Task<long> PortfolioRevisionAsync(int id, CancellationToken token, long minimum = 0) =>
        (await RequiredEventually(() => queries.GetPortfolioRevisionAsync(id, token), "Read Portfolio revision", token,
            value => value.Revision >= minimum).ConfigureAwait(false)).Revision;
    async Task<long> FundRevisionAsync(int portfolioId, int fundId, CancellationToken token, long minimum = 0) =>
        (await RequiredEventually(() => queries.GetFundRevisionAsync(portfolioId, fundId, token), "Read Fund revision", token,
            value => value.Revision >= minimum).ConfigureAwait(false)).Revision;

    static async Task<T> RequiredEventually<T>(
        Func<Task<ServiceResult<T>>> read,
        string operation,
        CancellationToken token,
        Func<T, bool>? accept = null) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var delay = TimeSpan.FromMilliseconds(50);
        ServiceResult<T>? last = null;
        do
        {
            token.ThrowIfCancellationRequested();
            last = await read().ConfigureAwait(false);
            if (last.Success && last.Value is not null && (accept is null || accept(last.Value))) return last.Value;
            await Task.Delay(delay, token).ConfigureAwait(false);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 500));
        } while (DateTime.UtcNow < deadline);
        throw new InvalidOperationException(operation + ": " + (last?.ErrorMessage ?? "projection did not become readable"));
    }
    static async Task RequireSuccess(Task<ServiceResult<Guid>> pending, string operation)
    {
        var result = await pending.ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException(operation + ": " + result.ErrorMessage);
    }
    static Task RequireFinancial(ServiceResult<GuidResult> result, string operation)
    {
        if (!result.Success) throw new InvalidOperationException(operation + ": " + result.ErrorMessage);
        return Task.CompletedTask;
    }

    static Guid StableId(string value) => DevelopmentTradingPortfolioDefaults.StableId(value);

    static DateTime PostgreSqlUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMicrosecond,
            DateTimeKind.Utc);
    }

    decimal FundCapital(TimeFrameType horizon) => FundCapitalByIndex(Array.IndexOf(DevelopmentTradingPortfolioOptions.Horizons, horizon));
    decimal FundCapitalByIndex(int index) => DevelopmentTradingPortfolioDefaults.CapitalAllocations(options.DevelopmentCapital)[index];

    static string DevelopmentDeploymentCode(CatalogKey key)
    {
        foreach (var horizon in DevelopmentTradingPortfolioOptions.Horizons)
            foreach (var family in new[] { "Future", "Vertical", "IronCondor" })
            {
                var code = $"Development{horizon}{family}";
                if (key == new CatalogKey(StrategyCatalogKind.Deployment, StrategyCatalogExamples.StableId(code), 1)) return code;
            }
        throw new InvalidOperationException("Unknown Development deployment key.");
    }

    static DevelopmentCatalog BuildCatalog(CatalogProduct futureProduct, CatalogProduct optionProduct)
    {
        var examples = StrategyCatalogExamples.Create();
        var family = StrategyCatalogExamples.New(StrategyCatalogKind.Family, "DevelopmentPaperFamily", "Development paper trading");
        var structures = new Dictionary<string, StrategyCatalogDefinition>();
        foreach (var source in examples.Where(x => x.Key.Kind == StrategyCatalogKind.Structure))
        {
            var item = StrategyCatalogExamples.New(StrategyCatalogKind.Structure, "Development" + source.Code, "Development " + source.Name) with
            { Legs = source.Legs, ExpiryGroups = source.ExpiryGroups, Capabilities = source.Capabilities };
            structures.Add(source.Code, item);
        }
        var strategy = StrategyCatalogExamples.New(StrategyCatalogKind.Strategy, "DevelopmentRegimeAligned", "Development regime-aligned strategy") with
        { Families = [family.Key], Structures = structures.Values.Select(x => x.Key).ToArray(), Capabilities = [new("evaluator", "RegimeAligned", 1), new("data", "AcceptedMarketAssessment", 1)] };
        var variants = new Dictionary<string, StrategyCatalogDefinition>();
        foreach (var source in examples.Where(x => x.Key.Kind == StrategyCatalogKind.Variant))
        {
            var structureCode = examples.Single(x => x.Key == source.Parent).Code;
            var isFuture = structureCode == "Future";
            var item = StrategyCatalogExamples.New(StrategyCatalogKind.Variant, "Development" + source.Code, "Development " + source.Name) with
            {
                Parent = structures[structureCode].Key, Side = source.Side, Bias = source.Bias, PremiumMode = source.PremiumMode,
                Capabilities = source.Capabilities, VariantLegs = source.VariantLegs,
                Settings = JsonSerializer.SerializeToElement(new
                {
                    TargetNetDelta = isFuture ? (source.Side == "Long" ? 1m : -1m) : source.Bias == "Balanced" ? 0m : source.Bias == "Bullish" ? .15m : -.15m,
                    BalanceTolerance = .05m, SymmetricWings = true, MinimumWingWidth = isFuture ? 0m : 5m,
                    MaximumWingWidth = isFuture ? 0m : 20m, DeltaUnits = "UnderlyingEquivalent"
                })
            };
            variants.Add(source.Code, item);
        }
        var schema = StrategyCatalogExamples.New(StrategyCatalogKind.ParameterSchema, "DevelopmentCompositionRulesSchema", "Development composition rules schema") with
        { Settings = CompositionRulesSchema.Settings(), Capabilities = [new("validator", CompositionRulesContract.Role, 1)] };
        var definitions = new List<StrategyCatalogDefinition> { family };
        definitions.AddRange(structures.Values); definitions.Add(strategy); definitions.AddRange(variants.Values); definitions.Add(schema);
        var deployments = new List<CatalogKey>();
        var byHorizon = new Dictionary<TimeFrameType, CatalogKey[]>();
        foreach (var horizon in DevelopmentTradingPortfolioOptions.Horizons)
        {
            var selection = TradeSelectionDefaultProfiles.EngineeringDefaults().Single(x => x.TargetHorizon == horizon);
            var construction = DevelopmentTradingPortfolioDefaults.ConstructionPolicies().Single(x => x.ParameterSetId == DevelopmentTradingPortfolioDefaults.ConstructionId(horizon));
            var risk = RiskParameterSet.Default(horizon);
            var horizonKeys = new List<CatalogKey>();
            foreach (var group in new[]
            {
                (Code: "Future", Variants: new[] { "LongFuture", "ShortFuture" }, Product: futureProduct),
                (Code: "Vertical", Variants: new[] { "BullCallDebit", "BearCallCredit", "BullPutCredit", "BearPutDebit" }, Product: optionProduct),
                (Code: "IronCondor", Variants: variants.Keys.Where(x => x.Contains("IronCondor", StringComparison.Ordinal)).ToArray(), Product: optionProduct)
            })
            {
                var selected = group.Variants.Select(x => variants[x]).ToArray();
                var rules = CompositionDefaultProfiles.Create(selected, horizon, new Black76ComposerPricer().Version);
                var parameter = StrategyCatalogExamples.New(StrategyCatalogKind.ParameterSet, $"Development{horizon}{group.Code}CompositionRules", $"Development {horizon} {group.Code} composition rules") with
                { Parent = schema.Key, Settings = JsonSerializer.SerializeToElement(rules) };
                definitions.Add(parameter);
                var deployment = StrategyCatalogExamples.New(StrategyCatalogKind.Deployment, $"Development{horizon}{group.Code}", $"Development {horizon} {group.Code}") with
                {
                    Parent = strategy.Key, Horizon = horizon, Variants = selected.Select(x => x.Key).ToArray(), Products = [group.Product],
                    Capabilities = [new("validator", "StructureVariant", 1)],
                    PipelineParameters =
                    [
                        new("selection-policy", CatalogPipelineParameterKind.TradeSelection, selection.ParameterSetId, 1, TradeSelectionPolicy.Hash(selection)),
                        new("composition-policy", CatalogPipelineParameterKind.OrderComposition, construction.ParameterSetId, 1, construction.Hash()),
                        new("risk-policy", CatalogPipelineParameterKind.RiskManagement, risk.ParameterSetId, 1, risk.Hash())
                    ],
                    Parameters = [new(CompositionRulesContract.Role, parameter.Key)]
                };
                definitions.Add(deployment); deployments.Add(deployment.Key); horizonKeys.Add(deployment.Key);
            }
            byHorizon.Add(horizon, [.. horizonKeys]);
        }
        return new([.. definitions], [.. deployments], byHorizon);
    }

    sealed record DevelopmentCatalog(StrategyCatalogDefinition[] Definitions, CatalogKey[] Deployments,
        IReadOnlyDictionary<TimeFrameType, CatalogKey[]> ByHorizon)
    {
        public CatalogKey[] For(TimeFrameType horizon) => ByHorizon[horizon];
    }
}
