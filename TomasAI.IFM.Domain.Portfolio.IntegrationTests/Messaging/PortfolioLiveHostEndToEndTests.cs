using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Explicit live-host qualification. It is excluded from the default Portfolio filter because it requires the API host.</summary>
public sealed class PortfolioLiveHostEndToEndTests
{
    [Fact]
    [Trait("Gate", "PF-22")]
    [Trait("Gate", "PF-26")]
    [Trait("Category", "PortfolioLiveHostReference")]
    public async Task Production_Reference_actor_returns_the_exact_read_only_v1_family_catalog()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"PortfolioReferenceTest{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var result = await new ReferenceQueryApi(producer).GetTradeStrategyFamiliesAsync(timeout.Token);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.Value.Should().NotBeNull();
            result.Value!.Select(x => (x.SystemKey, x.Name, x.DefinitionVersion, x.State)).Should().Equal(
                ("FUTURES", "Futures", 1L, TradeStrategyFamilyState.Active),
                ("VERTICAL_SPREAD", "Vertical Spread", 1L, TradeStrategyFamilyState.Active),
                ("IRON_CONDOR", "Iron Condor", 1L, TradeStrategyFamilyState.Active));
            result.Value.Should().OnlyContain(x => x.TradeStrategyFamilyId > 0);
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }

    [Fact]
    [Trait("Gate", "PF-02")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "PortfolioLiveHostIdentity")]
    public async Task Production_NATS_actor_allocates_all_typed_business_identities()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"PortfolioIdentityTest{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var identities = new PortfolioIdentityApi(producer);
            var allocations = new[]
            {
                await identities.AllocatePortfolioIdAsync(timeout.Token),
                await identities.AllocateFundIdAsync(timeout.Token),
                await identities.AllocatePolicyIdAsync(timeout.Token),
                await identities.AllocateOrderIdAsync(timeout.Token),
                await identities.AllocateTradeIdAsync(timeout.Token),
            };

            allocations.Should().OnlyContain(x => x.Success, string.Join("; ", allocations.Select(x => x.ErrorMessage)));
            allocations.Select(x => x.Value!.Kind).Should().Equal(
                PortfolioBusinessIdentityKind.Portfolio, PortfolioBusinessIdentityKind.Fund, PortfolioBusinessIdentityKind.Policy,
                PortfolioBusinessIdentityKind.Order, PortfolioBusinessIdentityKind.Trade);
            allocations.Should().OnlyContain(x => x.Value!.Value > 0 && x.Value.CorrelationId != Guid.Empty);
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }

    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Gate", "PF-05")]
    [Trait("Gate", "PF-06")]
    [Trait("Gate", "PF-09")]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-11")]
    [Trait("Gate", "PF-12")]
    [Trait("Gate", "PF-13")]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Gate", "PF-28")]
    [Trait("Category", "PortfolioLiveHostPipeline")]
    public async Task Production_NATS_actors_execute_configuration_resolution_reservation_composition_and_risk()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, $"PortfolioPipelineTest{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var portfolioId = EnvironmentValue("IFM_PORTFOLIO_LIVE_ID", Random.Shared.Next(1_000_000, 2_000_000_000));
            var fundId = EnvironmentValue("IFM_PORTFOLIO_FUND_LIVE_ID", Random.Shared.Next(1_000_000, 2_000_000_000));
            var workflowId = Guid.TryParse(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_LIVE_WORKFLOW_ID"), out var configuredWorkflow)
                ? configuredWorkflow
                : Guid.NewGuid();
            var now = DateTime.UtcNow;
            var policyAllocation = await new PortfolioIdentityApi(producer).AllocatePolicyIdAsync(timeout.Token);
            policyAllocation.Success.Should().BeTrue(policyAllocation.ErrorMessage);
            var policyId = policyAllocation.Value!.Value;
            var templateId = Guid.NewGuid();
            var hintProfileId = Guid.NewGuid();
            var compositionProfileId = Guid.NewGuid();
            var envelopeId = Guid.NewGuid();
            var portfolio = new PortfolioReadModel
            {
                PortfolioId = portfolioId,
                Name = "Live pipeline qualification",
                PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft,
                EffectiveFromUtc = now.AddMinutes(-1),
                CreatedOnUtc = now,
                CreatedBy = "portfolio-live-pipeline-test",
            };
            var policy = new PortfolioFinancialPolicyReadModel
            {
                PortfolioId = portfolioId, PolicyId = policyId, PolicyVersion = 1, Name = "Live pipeline limits",
                OperatingState = PortfolioFinancialPolicyState.Draft, BaseCurrency = "USD", CapitalBase = 1_000_000m,
                MaximumDeployableCapital = 900_000m, MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m,
                MaximumMargin = 500_000m, MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100,
                MaximumDrawdownAmount = 200_000m,
                TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000m, MaximumAggregateRisk = 50_000m, MaximumMargin = 250_000m, MaximumGrossNotional = 2_500_000m, MaximumOpenPositions = 50 }],
                EffectiveFromUtc = now.AddMinutes(-1), CreatedOnUtc = now, CreatedBy = "portfolio-live-pipeline-test"
            };
            var mandate = new FundMandateReadModel
            {
                PortfolioId = portfolioId,
                FundId = fundId,
                FundCode = $"F{fundId}",
                Name = "Daily ES Fund",
                FundMandateVersion = 1,
                TradingYear = now.Year,
                OperatingState = FundOperatingState.Draft,
                EffectiveFromUtc = now.AddMinutes(-1),
                DecisionHorizon = "Daily",
                Objective = "Directional ES exposure",
                UnderlyingUniverse = ["ES"],
                EligibleAssetTypes = ["Futures"],
                PermittedDirections = ["Long", "Short"],
                PermittedConditions = ["Trending"],
                PermittedTradeFamilies = ["DirectionalFuture"],
                CreatedOnUtc = now,
                CreatedBy = "portfolio-live-pipeline-test",
            };
            var assignment = new FundTradeTemplateAssignmentReadModel
            {
                PortfolioId = portfolioId,
                PortfolioVersion = 2,
                FundId = fundId,
                FundMandateVersion = 1,
                AssignmentVersion = 1,
                TradeTemplateId = templateId,
                TradeTemplateVersion = 1,
                Enabled = true,
                DecisionHorizon = "Daily",
                UnderlyingUniverse = ["ES"],
                AssetType = "Futures",
                TradeFamily = "DirectionalFuture",
                Priority = 1,
                EffectiveFromUtc = now.AddMinutes(-1),
                TradeSelectionHintProfileId = hintProfileId,
                TradeSelectionHintProfileVersion = 1,
                OrderCompositionProfileId = compositionProfileId,
                OrderCompositionProfileVersion = 1,
                CreatedOnUtc = now,
                CreatedBy = "portfolio-live-pipeline-test",
            };
            var allocation = new FundAllocationReadModel
            {
                PortfolioId = portfolioId,
                PortfolioVersion = 2,
                FundId = fundId,
                FundMandateVersion = 1,
                AllocationVersion = 1,
                TargetWeight = .5m,
                MaximumWeight = 1m,
                AllocatedCapital = 100_000m,
                Currency = "USD",
                EffectiveFromUtc = now.AddMinutes(-1),
                SourcePolicyId = policyId,
                SourcePolicyVersion = 1,
                CreatedOnUtc = now,
                CreatedBy = "portfolio-live-pipeline-test",
            };
            var envelope = new FundRiskEnvelopeReadModel
            {
                PortfolioId = portfolioId,
                PortfolioVersion = 2,
                FundId = fundId,
                FundMandateVersion = 1,
                EnvelopeId = envelopeId,
                EnvelopeVersion = 1,
                CapacityState = FundCapacityState.Available,
                Currency = "USD",
                AllocatedCapital = 100_000m,
                AvailableCapital = 90_000m,
                MaximumRiskPerTrade = 1_000m,
                MaximumAggregateRisk = 5_000m,
                MaximumMargin = 50_000m,
                MaximumGrossNotional = 500_000m,
                MaximumContracts = 10,
                MaximumOpenPositions = 5,
                RemainingLossBudget = 10_000m,
                EffectiveFromUtc = now.AddMinutes(-1),
                ExpiresAtUtc = now.AddMinutes(10),
                SourcePolicyId = policyId,
                SourcePolicyVersion = 1,
                CreatedOnUtc = now,
                CreatedBy = "portfolio-live-pipeline-test",
            };
            var portfolioCommands = new PortfolioCommandApi(producer);
            var policyCommands = new PortfolioFinancialPolicyCommandApi(producer);
            var queries = new PortfolioQueryApi(producer);
            var fundCommands = new PortfolioFundCommandApi(producer, queries);

            var portfolioCreateKey = Guid.NewGuid();
            await RequireSuccess(portfolioCommands.CreatePortfolioAsync(portfolio, portfolioCreateKey, timeout.Token));
            await RequireSuccess(portfolioCommands.CreatePortfolioAsync(portfolio, portfolioCreateKey, timeout.Token));
            var portfolioConflict = await portfolioCommands.CreatePortfolioAsync(portfolio with { Name = "Conflicting replay" }, portfolioCreateKey, timeout.Token);
            portfolioConflict.Success.Should().BeFalse();
            portfolioConflict.ErrorCode.Should().Be(PortfolioErrorCodes.IdempotencyConflict, portfolioConflict.ErrorMessage);
            await RequireSuccess(policyCommands.CreatePolicyAsync(policy, Guid.NewGuid(), timeout.Token));
            await RequireSuccess(policyCommands.ActivateAndAssignAsync(new(portfolioId, policyId), 1, 1, 1, timeout.Token));
            await RequireSuccess(portfolioCommands.AddFundAsync(new(portfolioId, fundId), 2, timeout.Token));
            await RequireSuccess(portfolioCommands.DelegateAllocationAsync(allocation, 3, timeout.Token));
            await RequireSuccess(portfolioCommands.DelegateRiskEnvelopeAsync(envelope, 4, timeout.Token));
            await RequireSuccess(portfolioCommands.ChangePortfolioStateAsync(new(portfolioId), 5, PortfolioOperatingState.Active, "live pipeline", timeout.Token));
            var fundCreateKey = Guid.NewGuid();
            await RequireSuccess(fundCommands.CreateFundMandateAsync(mandate, fundCreateKey, timeout.Token));
            await RequireSuccess(fundCommands.CreateFundMandateAsync(mandate, fundCreateKey, timeout.Token));
            var fundConflict = await fundCommands.CreateFundMandateAsync(mandate with { Name = "Conflicting replay" }, fundCreateKey, timeout.Token);
            fundConflict.Success.Should().BeFalse();
            fundConflict.ErrorCode.Should().Be(PortfolioErrorCodes.IdempotencyConflict, fundConflict.ErrorMessage);
            await RequireSuccess(fundCommands.AssignTradeTemplateAsync(assignment, 1, timeout.Token));
            await RequireSuccess(fundCommands.ChangeFundStateAsync(new(portfolioId, fundId), 2, FundOperatingState.Active, "configuration complete", timeout.Token));

            var manualRequest = new CreateManualFundOrderRequest
            {
                PortfolioId = portfolioId,
                PortfolioVersion = 2,
                FundId = fundId,
                FundMandateVersion = 1,
                UnderlyingRoot = "ES",
                RequestedTradeDate = DateOnly.FromDateTime(now),
                RequestedMaturityDate = DateOnly.FromDateTime(now.AddMonths(1)),
                Reference = "live manual Portfolio draft",
                IdempotencyKey = Guid.NewGuid(),
                RequestedAtUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(5),
            };
            var manual = await fundCommands.CreateManualOrderAsync(manualRequest, timeout.Token);
            manual.Success.Should().BeTrue(manual.ErrorMessage);
            manual.Value!.Order.OrderId.Should().BePositive();
            manual.Value.Order.Origin.Should().Be(CompositionOrigin.ManualUi);
            manual.Value.Order.Status.Should().Be(FundCompositionState.Draft.ToString());
            manual.Value.Trades.Should().BeEmpty();
            var manualReplay = await fundCommands.CreateManualOrderAsync(manualRequest, timeout.Token);
            manualReplay.Success.Should().BeTrue(manualReplay.ErrorMessage);
            manualReplay.Value!.Order.OrderId.Should().Be(manual.Value.Order.OrderId);
            manualReplay.Value.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);

            var snapshot = await WaitForSnapshotAsync(queries, portfolioId, now.Year, workflowId, timeout.Token);
            snapshot.Portfolio.PortfolioId.Should().Be(portfolioId);
            snapshot.Fund.FundId.Should().Be(fundId);
            snapshot.Assignments.Should().ContainSingle(x => x.TradeTemplateId == templateId);
            snapshot.RiskEnvelope.EnvelopeId.Should().Be(envelopeId);
            snapshot.FinancialPolicy.PolicyId.Should().Be(policyId);

            var request = new ReserveFundOrderCompositionRequest
            {
                WorkflowId = workflowId,
                WorkflowRevision = 1,
                TradeSelectionInvocationId = Guid.NewGuid(),
                TradeSelectionResultId = Guid.NewGuid(),
                TradeSelectionResultSha256 = new string('a', 64),
                PortfolioId = portfolioId,
                PortfolioVersion = 2,
                FundId = fundId,
                FundMandateVersion = 1,
                TradeTemplateId = templateId,
                TradeTemplateVersion = 1,
                OrderCompositionProfileId = compositionProfileId,
                OrderCompositionProfileVersion = 1,
                UnderlyingRoot = "ES",
                DecisionHorizon = "Daily",
                RequestedTradeDate = DateOnly.FromDateTime(now),
                TradeInstructions =
                [
                    new TradeInstruction
                    {
                        TradeFamily = "DirectionalFuture",
                        DirectionOrBias = "Long",
                        TradeAction = "Buy",
                        UnderlyingRoot = "ES",
                        RequestedTradeDate = DateOnly.FromDateTime(now),
                        Reference = "ES-primary",
                        CreatedOnUtc = now,
                        CreatedBy = "portfolio-live-pipeline-test",
                    },
                ],
                Origin = CompositionOrigin.StrategyWorkflow,
                IdempotencyKey = Guid.NewGuid(),
                RequestedAtUtc = now.AddSeconds(-1),
                ExpiresAtUtc = now.AddMinutes(5),
                PortfolioFundStrategySnapshotSha256 = snapshot.PayloadSha256,
            };

            var concurrent = await Task.WhenAll(
                fundCommands.ReserveCompositionAsync(request, snapshot, timeout.Token),
                fundCommands.ReserveCompositionAsync(request, snapshot, timeout.Token));
            concurrent.Should().OnlyContain(x => x.Success, string.Join("; ", concurrent.Select(x => x.ErrorMessage)));
            concurrent.Select(x => x.Value!.Order.OrderId).Distinct().Should().ContainSingle();
            concurrent.Select(x => x.Value!.Trades.Single().TradeId).Distinct().Should().ContainSingle();
            var reservation = concurrent[0].Value!;
            reservation.Order.OrderId.Should().BePositive();
            reservation.Trades.Should().ContainSingle(x => x.TradeId > 0);

            var replay = await fundCommands.ReserveCompositionAsync(request, snapshot, timeout.Token);
            replay.Success.Should().BeTrue(replay.ErrorMessage);
            replay.Value!.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);
            replay.Value.Order.OrderId.Should().Be(reservation.Order.OrderId);
            replay.Value.Trades.Single().TradeId.Should().Be(reservation.Trades.Single().TradeId);

            var orderId = new PortfolioFundOrderId(portfolioId, fundId, reservation.Order.OrderId);
            var composing = await fundCommands.MarkComposingAsync(orderId, 1, Guid.NewGuid(), timeout.Token);
            composing.Success.Should().BeTrue(composing.ErrorMessage);
            composing.Value!.Status.Should().Be(FundCompositionState.Composing.ToString());

            var compositionHash = new string('b', 64);
            var composed = await fundCommands.RecordComposedAsync(orderId, 2, new()
            {
                ResultId = Guid.NewGuid(),
                ResultSha256 = compositionHash,
                EvaluatedAtUtc = DateTime.UtcNow.AddMilliseconds(-1),
                ExpiresAtUtc = now.AddMinutes(5),
                InvocationId = Guid.NewGuid(),
            }, timeout.Token);
            composed.Success.Should().BeTrue(composed.ErrorMessage);
            composed.Value!.Status.Should().Be(FundCompositionState.RiskPending.ToString());

            var risk = await fundCommands.RecordRiskOutcomeAsync(orderId, 3, new()
            {
                ResultId = Guid.NewGuid(),
                ResultSha256 = new string('c', 64),
                Decision = RiskDecision.Approved,
                EvaluatedAtUtc = DateTime.UtcNow.AddMilliseconds(-1),
                ExpiresAtUtc = now.AddMinutes(5),
                EnvelopeId = envelopeId,
                EnvelopeVersion = 1,
                CandidateSha256 = compositionHash,
            }, timeout.Token);
            risk.Success.Should().BeTrue(risk.ErrorMessage);
            risk.Value!.Status.Should().Be(FundCompositionState.RiskApproved.ToString());
            risk.Value.OrderId.Should().Be(reservation.Order.OrderId);
            risk.Value.RiskResultHash.Should().Be(new string('c', 64));

            var authority = await new PortfolioEventStore(new PortfolioEventStoreFixture().EventSourceDb)
                .LoadFundAsync(new(portfolioId, fundId), timeout.Token);
            authority.Current!.OperatingState.Should().Be(FundOperatingState.Active);
            authority.Orders.Should().ContainSingle(x => x.OrderId == reservation.Order.OrderId && x.Status == FundCompositionState.RiskApproved.ToString());
            authority.Revision.Should().Be(8, "the manual draft and automated composition transitions share the Fund authority stream");
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }

    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "PortfolioLiveHost")]
    public async Task Production_NATS_actors_execute_create_read_update_read_with_real_projection()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, $"PortfolioLiveTest{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var portfolioId = int.TryParse(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_LIVE_ID"), out var configuredId)
                ? configuredId
                : Random.Shared.Next(1_000_000, 2_000_000_000);
            var now = DateTime.UtcNow;
            var model = new PortfolioReadModel
            {
                PortfolioId = portfolioId,
                Name = "Live host qualification",
                PortfolioVersion = 1,
                OperatingState = PortfolioOperatingState.Draft,
                ActivePolicyId = 9001,
                ActivePolicyVersion = 1,
                EffectiveFromUtc = now,
                CreatedOnUtc = now,
                CreatedBy = "portfolio-live-host-test",
            };
            var commands = new PortfolioCommandApi(producer);
            var queries = new PortfolioQueryApi(producer);

            var created = await commands.CreatePortfolioAsync(model, Guid.NewGuid(), timeout.Token);
            created.Success.Should().BeTrue(created.ErrorMessage);
            var eventSource = new PortfolioEventStoreFixture().EventSourceDb;
            var authority = await new PortfolioEventStore(eventSource).LoadPortfolioAsync(new PortfolioId(portfolioId), timeout.Token);
            authority.Current.Should().BeEquivalentTo(model);
            var firstRead = await WaitForPortfolioAsync(queries, eventSource, portfolioId, PortfolioOperatingState.Draft, timeout.Token);
            firstRead.PortfolioVersion.Should().Be(1);

            var changed = await commands.ChangePortfolioStateAsync(
                new PortfolioId(portfolioId),
                1,
                PortfolioOperatingState.Active,
                "live-host qualification",
                timeout.Token);
            changed.Success.Should().BeTrue(changed.ErrorMessage);
            var secondRead = await WaitForPortfolioAsync(queries, eventSource, portfolioId, PortfolioOperatingState.Active, timeout.Token);
            secondRead.PortfolioId.Should().Be(portfolioId);
            secondRead.OperatingState.Should().Be(PortfolioOperatingState.Active);
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    [Trait("Gate", "PF-09")]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "PortfolioLiveHostRestart")]
    public async Task Production_NATS_query_and_authority_retain_state_after_host_restart()
    {
        var portfolioId = int.Parse(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_LIVE_ID")
            ?? throw new InvalidOperationException("IFM_PORTFOLIO_LIVE_ID must identify the pre-restart Portfolio."));
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"PortfolioRestartTest{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var eventSource = new PortfolioEventStoreFixture().EventSourceDb;
            var projected = await WaitForPortfolioAsync(new PortfolioQueryApi(producer), eventSource, portfolioId, PortfolioOperatingState.Active, timeout.Token);
            var authority = await new PortfolioEventStore(eventSource).LoadPortfolioAsync(new PortfolioId(portfolioId), timeout.Token);

            projected.Should().BeEquivalentTo(authority.Current);
            authority.Revision.Should().Be(2);
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }

    [Fact]
    [Trait("Gate", "PF-04")]
    [Trait("Gate", "PF-05")]
    [Trait("Gate", "PF-06")]
    [Trait("Gate", "PF-09")]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-11")]
    [Trait("Gate", "PF-12")]
    [Trait("Gate", "PF-14")]
    [Trait("Gate", "PF-15")]
    [Trait("Category", "PortfolioLiveHostPipelineRestart")]
    public async Task Production_pipeline_configuration_and_composition_retain_state_after_host_restart()
    {
        var portfolioId = EnvironmentValue("IFM_PORTFOLIO_LIVE_ID", 0);
        var fundId = EnvironmentValue("IFM_PORTFOLIO_FUND_LIVE_ID", 0);
        var workflowId = Guid.Parse(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_LIVE_WORKFLOW_ID")
            ?? throw new InvalidOperationException("IFM_PORTFOLIO_LIVE_WORKFLOW_ID must identify the pre-restart workflow."));
        portfolioId.Should().BePositive("IFM_PORTFOLIO_LIVE_ID must identify the pre-restart Portfolio");
        fundId.Should().BePositive("IFM_PORTFOLIO_FUND_LIVE_ID must identify the pre-restart Fund");
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"PortfolioPipelineRestart{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var queries = new PortfolioQueryApi(producer);
            var projectedPortfolio = await WaitForPortfolioAsync(
                queries,
                new PortfolioEventStoreFixture().EventSourceDb,
                portfolioId,
                PortfolioOperatingState.Active,
                timeout.Token);
            var fund = await queries.GetFundAsync(portfolioId, fundId, cancellationToken: timeout.Token);
            fund.Success.Should().BeTrue(fund.ErrorMessage);
            fund.Value!.OperatingState.Should().Be(FundOperatingState.Active);
            var snapshot = await WaitForSnapshotAsync(queries, portfolioId, DateTime.UtcNow.Year, workflowId, timeout.Token);
            snapshot.Portfolio.Should().BeEquivalentTo(projectedPortfolio);
            snapshot.Fund.Should().BeEquivalentTo(fund.Value);

            var workflow = await queries.GetCompositionByWorkflowAsync(workflowId, timeout.Token);
            workflow.Success.Should().BeTrue(workflow.ErrorMessage);
            var reference = workflow.Value.Should().ContainSingle(x => x.PortfolioId == portfolioId && x.FundId == fundId).Subject;
            var projectedOrder = await queries.GetOrderAsync(reference.OrderId, timeout.Token);
            projectedOrder.Success.Should().BeTrue(projectedOrder.ErrorMessage);
            projectedOrder.Value!.Status.Should().Be(FundCompositionState.RiskApproved.ToString());
            var projectedTrades = await queries.GetOrderTradesAsync(reference.OrderId, 16, cancellationToken: timeout.Token);
            projectedTrades.Success.Should().BeTrue(projectedTrades.ErrorMessage);
            projectedTrades.Value!.Items.Should().ContainSingle(x => x.TradeId > 0);

            var authority = await new PortfolioEventStore(new PortfolioEventStoreFixture().EventSourceDb)
                .LoadFundAsync(new(portfolioId, fundId), timeout.Token);
            authority.Revision.Should().Be(7);
            projectedOrder.Value.Should().BeEquivalentTo(authority.Orders.Single(x => x.OrderId == reference.OrderId));
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }

    static async Task<PortfolioReadModel> WaitForPortfolioAsync(
        PortfolioQueryApi queries,
        TomasAI.IFM.Application.Storage.EventSourceDb.EventSourceActorDbContext eventSource,
        int portfolioId,
        PortfolioOperatingState expectedState,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var result = await queries.GetPortfolioAsync(portfolioId, cancellationToken: cancellationToken);
            if (result.Success && result.Value?.OperatingState == expectedState) return result.Value;
            await Task.Delay(100, cancellationToken);
        }
        var stream = await eventSource.GetEventStreamIdFromDbAsync(PortfolioEventStore.PortfolioStream(new PortfolioId(portfolioId)));
        var persisted = stream is null ? [] : await eventSource.LoadActorEventStreamAsync<DiagnosticState>(stream.EventStreamId);
        var eventId = persisted.OrderBy(x => x.EventVersion).LastOrDefault()?.EventVersion ?? 0;
        var projector = eventId == 0 ? null : await eventSource.GetEventProjectorStateAsync(eventId, "PortfolioEventProjector");
        throw new TimeoutException($"Portfolio {portfolioId} did not project state {expectedState}; eventId={eventId}, projectorOutcome={projector?.Outcome}, projectorStage={projector?.Stage}, projectorError={projector?.ErrorMessage}.");
    }

    static async Task<PortfolioFundStrategySnapshot> WaitForSnapshotAsync(
        PortfolioQueryApi queries,
        int portfolioId,
        int tradingYear,
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        ServiceResult<PortfolioFundStrategySnapshot>? last = null;
        for (var attempt = 0; attempt < 80; attempt++)
        {
            last = await queries.GetStrategySnapshotAsync(portfolioId, tradingYear, "Daily", "ES", "Futures", DateTime.UtcNow, workflowId, 1, Guid.NewGuid(), cancellationToken);
            if (last.Success && last.Value is not null) return last.Value;
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException($"Strategy snapshot did not become query-visible: {last?.ErrorCode} {last?.ErrorMessage}");
    }

    static int EnvironmentValue(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var configured) ? configured : fallback;

    static async Task RequireSuccess(Task<ServiceResult<Guid>> operation)
    {
        var result = await operation;
        result.Success.Should().BeTrue(result.ErrorMessage);
    }

    sealed class DiagnosticState : IActorState<DiagnosticState>
    {
        public ActorThreadId Id { get; set; }
    }
}
