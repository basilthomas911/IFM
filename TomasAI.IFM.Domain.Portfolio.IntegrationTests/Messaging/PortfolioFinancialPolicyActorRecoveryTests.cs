using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class PortfolioFinancialPolicyActorRecoveryTests
{
    [Fact]
    [Trait("Category", "Portfolio")]
    [Trait("Gate", "PF-24")]
    public async Task Retry_heals_assignment_and_projections_after_policy_commit_partial_failure()
    {
        var store = new InMemoryPolicyStore();
        var policyId = new PortfolioFinancialPolicyId(701, 8101);
        store.SeedPortfolio(Portfolio(701));
        store.SeedPolicy(policyId, Policy(701, 8101));
        store.FailNextPortfolioAppend = true;
        var (actor, context, projections, projector) = CreateActor(store);
        var command = Activation(policyId, Guid.NewGuid(), expectedPolicyRevision: 1, expectedPortfolioRevision: 1);
        var typed = (ICommandActor<PortfolioFinancialPolicyCommandActor>)actor;
        var firstState = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);

        var firstAttempt = async () => await typed.ReceiveAsync(context, firstState, command);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>().WithMessage("*injected*");
        store.PolicyHistory(policyId).Should().HaveCount(2, "the policy activation committed before the injected Portfolio failure");
        (await store.LoadPortfolioAsync(new(701))).Current!.ActivePolicyId.Should().Be(0, "the prior Portfolio assignment remains valid until healing succeeds");

        var retryState = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
        var retry = await typed.ReceiveAsync(context, retryState, command);

        retry.Success.Should().BeTrue();
        store.PolicyHistory(policyId).Should().HaveCount(2, "retry must not duplicate the committed activation");
        var healed = await store.LoadPortfolioAsync(new(701));
        healed.Current!.ActivePolicyId.Should().Be(8101);
        healed.Current.ActivePolicyVersion.Should().Be(1);
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Single() is PortfolioFinancialPolicyActivated));
        await projections.Received().UpsertPortfolioAsync(Arg.Any<PortfolioProjection<PortfolioReadModel>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await projections.Received().UpsertPolicyAsync(Arg.Any<PortfolioProjection<PortfolioFinancialPolicyReadModel>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Portfolio")]
    [Trait("Gate", "PF-24")]
    public async Task Concurrent_policy_replacements_commit_one_logical_assignment_without_orphan_activation()
    {
        var store = new InMemoryPolicyStore();
        var firstId = new PortfolioFinancialPolicyId(702, 8201);
        var secondId = new PortfolioFinancialPolicyId(702, 8202);
        store.SeedPortfolio(Portfolio(702));
        store.SeedPolicy(firstId, Policy(702, 8201));
        store.SeedPolicy(secondId, Policy(702, 8202));
        var firstActor = CreateActor(store);
        var secondActor = CreateActor(store);
        var firstCommand = Activation(firstId, Guid.NewGuid(), 1, 1);
        var secondCommand = Activation(secondId, Guid.NewGuid(), 1, 1);
        var firstTyped = (ICommandActor<PortfolioFinancialPolicyCommandActor>)firstActor.Actor;
        var secondTyped = (ICommandActor<PortfolioFinancialPolicyCommandActor>)secondActor.Actor;
        var firstState = await firstTyped.OnLoadStateAsync(firstActor.Context, firstCommand.Subject.ThreadId, firstCommand);
        var secondState = await secondTyped.OnLoadStateAsync(secondActor.Context, secondCommand.Subject.ThreadId, secondCommand);

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => firstTyped.ReceiveAsync(firstActor.Context, firstState, firstCommand).AsTask()),
            CaptureAsync(() => secondTyped.ReceiveAsync(secondActor.Context, secondState, secondCommand).AsTask()));

        outcomes.Count(x => x.Success).Should().Be(1);
        outcomes.Count(x => x.Error is InvalidOperationException).Should().Be(1);
        store.PolicyHistory(firstId).Concat(store.PolicyHistory(secondId)).OfType<PortfolioFinancialPolicyActivated>().Should().ContainSingle();
        var portfolio = await store.LoadPortfolioAsync(new(702));
        portfolio.Revision.Should().Be(2);
        portfolio.Current!.ActivePolicyId.Should().BeOneOf(8201, 8202);
    }

    static async Task<(bool Success, Exception? Error)> CaptureAsync(Func<Task<ServiceResult<GuidResult>>> operation)
    {
        try { return ((await operation()).Success, null); }
        catch (Exception exception) { return (false, exception); }
    }

    static (PortfolioFinancialPolicyCommandActor Actor,
        ICommandActorContext<PortfolioFinancialPolicyCommandActor> Context,
        IPortfolioDbWriteContext Projections,
        IEventProjector<PortfolioFinancialPolicyCommandActor> Projector) CreateActor(IPortfolioEventStore store)
    {
        var context = Substitute.For<ICommandActorContext<PortfolioFinancialPolicyCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, PortfolioFinancialPolicyCommandActor.ActorName));
        var projections = Substitute.For<IPortfolioDbWriteContext>();
        var projector = Substitute.For<IEventProjector<PortfolioFinancialPolicyCommandActor>>();
        return (new(context, store, projections, projector, Substitute.For<ILogger<PortfolioFinancialPolicyCommandActor>>()), context, projections, projector);
    }

    static PortfolioCommand<ActivateAndAssignPortfolioFinancialPolicyPayload, PortfolioFinancialPolicyId> Activation(
        PortfolioFinancialPolicyId id, Guid commandId, long expectedPolicyRevision, long expectedPortfolioRevision) => new()
    {
        CommandId = commandId,
        EntityId = id,
        ErrorCode = 34020,
        Subject = new(ActorType.Command, PortfolioFinancialPolicyCommandActor.ActorName, "ActivateAndAssignPortfolioFinancialPolicy", id.Format()),
        Payload = new(1, expectedPolicyRevision, expectedPortfolioRevision),
    };

    static PortfolioReadModel Portfolio(int id) => new()
    {
        PortfolioId = id, PortfolioVersion = 1, Name = $"Portfolio {id}", OperatingState = PortfolioOperatingState.Draft,
        EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1), CreatedOnUtc = DateTime.UtcNow, CreatedBy = "integration"
    };

    static PortfolioFinancialPolicyReadModel Policy(int portfolioId, int policyId) => new()
    {
        PortfolioId = portfolioId, PolicyId = policyId, PolicyVersion = 1, Name = $"Policy {policyId}",
        OperatingState = PortfolioFinancialPolicyState.Draft, BaseCurrency = "USD", CapitalBase = 1_000_000,
        MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000, MaximumAggregateRisk = 100_000,
        MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000, MaximumOpenPositions = 100,
        MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
        EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1), CreatedOnUtc = DateTime.UtcNow, CreatedBy = "integration"
    };

    sealed class InMemoryPolicyStore : IPortfolioEventStore
    {
        readonly object sync = new();
        readonly Dictionary<int, List<PortfolioDomainEvent>> portfolios = [];
        readonly Dictionary<PortfolioFinancialPolicyId, List<PortfolioFinancialPolicyDomainEvent>> policies = [];
        public bool FailNextPortfolioAppend { get; set; }

        public void SeedPortfolio(PortfolioReadModel model)
        {
            var aggregate = new PortfolioAggregate();
            var created = aggregate.Create(Guid.NewGuid(), model, DateTime.UtcNow, "seed");
            portfolios[model.PortfolioId] = [created];
        }

        public void SeedPolicy(PortfolioFinancialPolicyId id, PortfolioFinancialPolicyReadModel model)
        {
            var aggregate = new PortfolioFinancialPolicyAggregate();
            var created = aggregate.Create(Guid.NewGuid(), Guid.NewGuid(), model, DateTime.UtcNow, "seed");
            policies[id] = [created];
        }

        public IReadOnlyList<PortfolioFinancialPolicyDomainEvent> PolicyHistory(PortfolioFinancialPolicyId id)
        { lock (sync) return policies[id].ToArray(); }

        public Task AppendPortfolioAsync(PortfolioId id, PortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                var history = portfolios[id.Id];
                if (history.Count != expectedRevision) throw new InvalidOperationException($"Expected Portfolio revision {expectedRevision}, actual {history.Count}.");
                if (FailNextPortfolioAppend) { FailNextPortfolioAppend = false; throw new InvalidOperationException("injected Portfolio append failure"); }
                history.Add(domainEvent);
            }
            return Task.CompletedTask;
        }

        public Task AppendPolicyAsync(PortfolioFinancialPolicyId id, PortfolioFinancialPolicyDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                var history = policies[id];
                if (history.Count != expectedRevision) throw new InvalidOperationException($"Expected policy revision {expectedRevision}, actual {history.Count}.");
                history.Add(domainEvent);
            }
            return Task.CompletedTask;
        }

        public Task<PortfolioAggregate> LoadPortfolioAsync(PortfolioId id, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                var aggregate = new PortfolioAggregate();
                aggregate.Replay(portfolios[id.Id].ToArray());
                return Task.FromResult(aggregate);
            }
        }

        public Task<PortfolioFinancialPolicyAggregate> LoadPolicyAsync(PortfolioFinancialPolicyId id, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                var aggregate = new PortfolioFinancialPolicyAggregate();
                aggregate.Replay(policies[id].ToArray());
                return Task.FromResult(aggregate);
            }
        }

        public Task<PortfolioFinancialPolicyDomainEvent?> FindCommittedPolicyCommandAsync(PortfolioFinancialPolicyId id, Guid commandId, CancellationToken cancellationToken = default)
        { lock (sync) return Task.FromResult(policies[id].SingleOrDefault(x => x.CommandId == commandId)); }
        public Task<IReadOnlyList<PortfolioFinancialPolicyDomainEvent>> LoadPolicyHistoryAsync(PortfolioFinancialPolicyId id, CancellationToken cancellationToken = default) => Task.FromResult(PolicyHistory(id));
        public Task<IReadOnlyList<PortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId id, CancellationToken cancellationToken = default)
        { lock (sync) return Task.FromResult<IReadOnlyList<PortfolioDomainEvent>>(portfolios[id.Id].ToArray()); }
        public Task AppendFundAsync(PortfolioFundId fundId, PortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioDomainEvent?>(null);
        public Task<PortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioFundDomainEvent?>(null);
        public Task<PortfolioCreated?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioCreated?>(null);
        public Task<FundMandateCreated?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<FundMandateCreated?>(null);
        public Task<IReadOnlyList<PortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortfolioFundDomainEvent>>([]);
    }
}
