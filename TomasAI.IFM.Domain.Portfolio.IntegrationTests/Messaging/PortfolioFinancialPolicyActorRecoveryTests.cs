using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
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
    public async Task Retry_commits_policy_and_assignment_once_after_atomic_commit_failure()
    {
        var store = new InMemoryPolicyStore();
        var policyId = new PortfolioFinancialPolicyId(701, 8101);
        store.SeedPortfolio(Portfolio(701));
        store.SeedPolicy(policyId, Policy(701, 8101));
        store.FailNextPortfolioAppend = true;
        var (actor, context, portfolioProjector, projector) = CreateActor(store);
        var parent = (await store.LoadPortfolioAsync(new(701))).Current!;
        var command = Activation(policyId, Guid.NewGuid(), 1, 1, parent);
        var typed = (ICommandActor<PortfolioFinancialPolicyCommandActor>)actor;
        var firstState = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);

        var firstAttempt = async () => await typed.ReceiveAsync(context, firstState, command);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>().WithMessage("*injected*");
        store.PolicyHistory(policyId).Should().HaveCount(1, "the atomic policy/Portfolio batch rolls back completely");
        (await store.LoadPortfolioAsync(new(701))).Current!.ActivePolicyId.Should().Be(0, "the prior Portfolio assignment remains valid until healing succeeds");

        var retryState = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
        var retry = await typed.ReceiveAsync(context, retryState, command);

        retry.Success.Should().BeTrue();
        store.PolicyHistory(policyId).Should().HaveCount(2, "retry must not duplicate the committed activation");
        var healed = await store.LoadPortfolioAsync(new(701));
        healed.Current!.ActivePolicyId.Should().Be(8101);
        healed.Current.ActivePolicyVersion.Should().Be(1);
        await projector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Single() is PortfolioFinancialPolicyActivatedEvent));
        await portfolioProjector.Received(1).DomainEventsProjectionAsync(Arg.Is<DomainEventCollection>(x => x.Single() is PortfolioFinancialPolicyAssignedEvent));
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
        var parent = (await store.LoadPortfolioAsync(new(702))).Current!;
        var firstCommand = Activation(firstId, Guid.NewGuid(), 1, 1, parent);
        var secondCommand = Activation(secondId, Guid.NewGuid(), 1, 1, parent);
        var firstTyped = (ICommandActor<PortfolioFinancialPolicyCommandActor>)firstActor.Actor;
        var secondTyped = (ICommandActor<PortfolioFinancialPolicyCommandActor>)secondActor.Actor;
        var firstState = await firstTyped.OnLoadStateAsync(firstActor.Context, firstCommand.Subject.ThreadId, firstCommand);
        var secondState = await secondTyped.OnLoadStateAsync(secondActor.Context, secondCommand.Subject.ThreadId, secondCommand);

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => firstTyped.ReceiveAsync(firstActor.Context, firstState, firstCommand).AsTask()),
            CaptureAsync(() => secondTyped.ReceiveAsync(secondActor.Context, secondState, secondCommand).AsTask()));

        outcomes.Count(x => x.Success).Should().Be(1);
        outcomes.Count(x => x.Error is InvalidOperationException).Should().Be(1);
        store.PolicyHistory(firstId).Concat(store.PolicyHistory(secondId)).OfType<PortfolioFinancialPolicyActivatedEvent>().Should().ContainSingle();
        var portfolio = await store.LoadPortfolioAsync(new(702));
        portfolio.Revision.Should().Be(2);
        portfolio.Current!.ActivePolicyId.Should().BeOneOf(8201, 8202);
    }

    [Fact]
    [Trait("Category", "Portfolio")]
    public async Task Committed_activation_survives_projection_submission_failure_and_retries_without_reassigning()
    {
        var store = new InMemoryPolicyStore();
        var firstId = new PortfolioFinancialPolicyId(703, 8301);
        var secondId = new PortfolioFinancialPolicyId(703, 8302);
        store.SeedPortfolio(Portfolio(703));
        store.SeedPolicy(firstId, Policy(703, 8301));
        store.SeedPolicy(secondId, Policy(703, 8302));
        var (actor, context, portfolioProjector, projector) = CreateActor(store);
        projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>())
            .Returns(_ => ValueTask.FromException(new InvalidOperationException("Projector submission failed.")));
        var typed = (ICommandActor<PortfolioFinancialPolicyCommandActor>)actor;
        var first = Activation(firstId, Guid.NewGuid(), 1, 1, (await store.LoadPortfolioAsync(new(703))).Current!);
        var forged = first with { ParentPortfolio = first.ParentPortfolio! with { Name = "Different Portfolio facts" } };
        var rejected = async () => await ReceiveAsync(forged);
        await rejected.Should().ThrowAsync<InvalidOperationException>().WithMessage("*disagrees*committed revision*");
        store.PolicyHistory(firstId).Should().HaveCount(1);

        await ReceiveAsync(first);
        var second = Activation(secondId, Guid.NewGuid(), 1, 2, (await store.LoadPortfolioAsync(new(703))).Current!);
        await ReceiveAsync(second);
        await ReceiveAsync(first);

        store.PolicyHistory(firstId).OfType<PortfolioFinancialPolicyActivatedEvent>().Should().ContainSingle();
        var portfolio = await store.LoadPortfolioAsync(new(703));
        portfolio.Revision.Should().Be(3);
        portfolio.Current!.ActivePolicyId.Should().Be(8302);
        await portfolioProjector.Received(3).DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>());

        async Task ReceiveAsync(ActivateAndAssignPortfolioFinancialPolicyCommand command)
        {
            var state = await typed.OnLoadStateAsync(context, command.Subject.ThreadId, command);
            (await typed.ReceiveAsync(context, state, command)).Success.Should().BeTrue();
        }
    }

    static async Task<(bool Success, Exception? Error)> CaptureAsync(Func<Task<ServiceResult<GuidResult>>> operation)
    {
        try { return ((await operation()).Success, null); }
        catch (Exception exception) { return (false, exception); }
    }

    static (PortfolioFinancialPolicyCommandActor Actor,
        ICommandActorContext<PortfolioFinancialPolicyCommandActor> Context,
        IEventProjector<PortfolioCommandActor> PortfolioProjector,
        IEventProjector<PortfolioFinancialPolicyCommandActor> Projector) CreateActor(IPortfolioEventStore store)
    {
        var context = Substitute.For<ICommandActorContext<PortfolioFinancialPolicyCommandActor>>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Command, PortfolioFinancialPolicyCommandActor.ActorName));
        var portfolioProjector = Substitute.For<IEventProjector<PortfolioCommandActor>>();
        var projector = Substitute.For<IEventProjector<PortfolioFinancialPolicyCommandActor>>();
        return (new(context, store, portfolioProjector, projector,
            new PortfolioOperationalGuard(new PortfolioOperationalOptions()),
            Substitute.For<ILogger<PortfolioFinancialPolicyCommandActor>>()), context, portfolioProjector, projector);
    }

    static ActivateAndAssignPortfolioFinancialPolicyCommand Activation(
        PortfolioFinancialPolicyId id, Guid commandId, long expectedPolicyRevision, long expectedPortfolioRevision, PortfolioReadModel parent) => new()
        {
            CommandId = commandId,
            EntityId = id,
            ErrorCode = 34020,
            Subject = new(ActorType.Command, PortfolioFinancialPolicyCommandActor.ActorName, "ActivateAndAssignPortfolioFinancialPolicy", id.Format()),
            PolicyVersion = 1,
            ExpectedPolicyRevision = expectedPolicyRevision,
            ExpectedPortfolioRevision = expectedPortfolioRevision,
            ParentPortfolio = parent,
            ReferencePreparationId = Guid.NewGuid(),
            Access = PortfolioAccessContext.Administrator("integration-admin"),
        };

    static PortfolioReadModel Portfolio(int id) => new()
    {
        PortfolioId = id,
        PortfolioVersion = 1,
        Name = $"Portfolio {id}",
        OperatingState = PortfolioOperatingState.Draft,
        EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        CreatedOnUtc = DateTime.UtcNow,
        CreatedBy = "integration"
    };

    static PortfolioFinancialPolicyReadModel Policy(int portfolioId, int policyId) => new()
    {
        PortfolioId = portfolioId,
        PolicyId = policyId,
        PolicyVersion = 1,
        Name = $"Policy {policyId}",
        OperatingState = PortfolioFinancialPolicyState.Draft,
        BaseCurrency = "USD",
        CapitalBase = 1_000_000,
        MaximumDeployableCapital = 900_000,
        MaximumRiskPerTrade = 10_000,
        MaximumAggregateRisk = 100_000,
        MaximumMargin = 500_000,
        MaximumGrossNotional = 5_000_000,
        MaximumOpenPositions = 100,
        MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
        EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        CreatedOnUtc = DateTime.UtcNow,
        CreatedBy = "integration"
    };

    sealed class InMemoryPolicyStore : IPortfolioEventStore
    {
        public Task VerifyReferencePreparationAsync(ValidatePortfolioReferenceCommand request, CancellationToken cancellationToken = default)
        {
            request.ActivatesPolicy.Should().BeTrue();
            request.Policy.Should().NotBeNull();
            return Task.CompletedTask;
        }

        readonly object sync = new();
        readonly Dictionary<int, List<IPortfolioDomainEvent>> portfolios = [];
        readonly Dictionary<PortfolioFinancialPolicyId, List<IPortfolioFinancialPolicyDomainEvent>> policies = [];
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

        public IReadOnlyList<IPortfolioFinancialPolicyDomainEvent> PolicyHistory(PortfolioFinancialPolicyId id)
        { lock (sync) return policies[id].ToArray(); }

        public Task AppendPolicyAndPortfolioAsync(PortfolioFinancialPolicyId policyId, IPortfolioFinancialPolicyDomainEvent policyEvent,
            PortfolioId portfolioId, IPortfolioDomainEvent portfolioEvent, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                var policyHistory = policies[policyId];
                var portfolioHistory = portfolios[portfolioId.Id];
                if (policyHistory.Count != policyEvent.Revision - 1 || portfolioHistory.Count != portfolioEvent.Revision - 1)
                    throw new InvalidOperationException("Concurrent policy or Portfolio revision.");
                if (FailNextPortfolioAppend) { FailNextPortfolioAppend = false; throw new InvalidOperationException("injected atomic append failure"); }
                policyHistory.Add(policyEvent);
                portfolioHistory.Add(portfolioEvent);
            }
            return Task.CompletedTask;
        }

        public Task AppendPortfolioAsync(PortfolioId id, IPortfolioDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
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

        public Task AppendPolicyAsync(PortfolioFinancialPolicyId id, IPortfolioFinancialPolicyDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default)
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

        public Task<IPortfolioFinancialPolicyDomainEvent?> FindCommittedPolicyCommandAsync(PortfolioFinancialPolicyId id, Guid commandId, CancellationToken cancellationToken = default)
        { lock (sync) return Task.FromResult(policies[id].SingleOrDefault(x => x.CommandId == commandId)); }
        public Task<IReadOnlyList<IPortfolioFinancialPolicyDomainEvent>> LoadPolicyHistoryAsync(PortfolioFinancialPolicyId id, CancellationToken cancellationToken = default) => Task.FromResult(PolicyHistory(id));
        public Task<IReadOnlyList<IPortfolioDomainEvent>> LoadPortfolioHistoryAsync(PortfolioId id, CancellationToken cancellationToken = default)
        { lock (sync) return Task.FromResult<IReadOnlyList<IPortfolioDomainEvent>>(portfolios[id.Id].ToArray()); }
        public Task AppendFundAsync(PortfolioFundId fundId, IPortfolioFundDomainEvent domainEvent, long expectedRevision, PortfolioEventMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortfolioFundAggregate> LoadFundAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SavePortfolioSnapshotAsync(PortfolioId portfolioId, PortfolioAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveFundSnapshotAsync(PortfolioFundId fundId, PortfolioFundAggregate aggregate, DateTime nowUtc, string principal, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IPortfolioDomainEvent?> FindCommittedPortfolioCommandAsync(PortfolioId portfolioId, Guid commandId, CancellationToken cancellationToken = default)
        { lock (sync) return Task.FromResult(portfolios[portfolioId.Id].SingleOrDefault(x => x.CommandId == commandId)); }
        public Task<IPortfolioFundDomainEvent?> FindCommittedFundCommandAsync(PortfolioFundId fundId, Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult<IPortfolioFundDomainEvent?>(null);
        public Task<PortfolioCreatedEvent?> FindPortfolioCreateByIdempotencyKeyAsync(PortfolioId portfolioId, Guid idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<PortfolioCreatedEvent?>(null);
        public Task<FundMandateCreatedEvent?> FindFundCreateByIdempotencyKeyAsync(PortfolioFundId fundId, Guid idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<FundMandateCreatedEvent?>(null);
        public Task<IReadOnlyList<IPortfolioFundDomainEvent>> LoadFundHistoryAsync(PortfolioFundId fundId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IPortfolioFundDomainEvent>>([]);
    }
}
