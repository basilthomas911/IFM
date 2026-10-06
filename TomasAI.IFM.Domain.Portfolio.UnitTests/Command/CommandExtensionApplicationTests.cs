using System.Reflection;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Domain.Portfolio.Command;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class CommandExtensionApplicationTests
{
    static readonly DateTime Now = new(2026, 8, 29, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Portfolio_computation_does_not_mutate_until_the_event_is_applied_and_replays_identically()
    {
        var state = new PortfolioAggregate();
        var command = new CreatePortfolioCommand(PortfolioAggregateTests.Draft(), Guid.NewGuid()) with { CommandId = Guid.NewGuid() };
        Assert.True(command.Compute(state, Now, "admin", out var portfolioChange));
        Assert.Null(state.Current);
        Assert.Equal(0, state.Revision);
        Assert.Null(state.PendingEvent);
        var created = command.CreatePortfolioCreatedEvent(portfolioChange);
        Assert.Equal(command.CommandId, created.CommandId);
        Assert.Equal(command.IdempotencyKey, created.IdempotencyKey);
        Assert.True(state.Update(created, command));
        var restored = new PortfolioAggregate();
        restored.Replay([created]);
        Assert.Equal(state.Current, restored.Current);
        Assert.Equal(state.Revision, restored.Revision);
    }

    [Fact]
    public void Rejected_Portfolio_transition_leaves_state_and_pending_events_unchanged()
    {
        var state = new PortfolioAggregate();
        var create = new CreatePortfolioCommand(PortfolioAggregateTests.Draft(), Guid.NewGuid()) with { CommandId = Guid.NewGuid() };
        Assert.True(create.Execute(state, Now, "admin").Success);
        var before = state.CaptureSnapshot();
        var pending = state.PendingEvent;
        var change = new ChangePortfolioOperatingStateCommand(99, PortfolioOperatingState.Active, "stale") with { CommandId = Guid.NewGuid() };
        var rejection = change.Execute(state, Now, "admin");
        Assert.False(rejection.Success);
        Assert.Contains("revision", rejection.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before.Current, state.Current);
        Assert.Equal(before.Revision, state.Revision);
        Assert.Same(pending, state.PendingEvent);
    }

    [Fact]
    public void Fund_composition_is_calculated_without_inserting_an_order_before_event_application()
    {
        var state = new PortfolioFundAggregate();
        var createdFund = state.Create(Guid.NewGuid(), PortfolioFundAggregateTests.Draft(), Now, "admin");
        var activatedFund = state.ChangeState(Guid.NewGuid(), 1, FundOperatingState.Active, "activate", new(true, 1, true, true), Now, "admin");
        var request = new CreateManualFundOrderRequest
        {
            PortfolioId = 101, PortfolioVersion = 1, FundId = 205, FundMandateVersion = 1,
            Reference = "manual draft", IdempotencyKey = Guid.NewGuid(), RequestedAtUtc = Now,
            ExpiresAtUtc = Now.AddDays(1)
        };
        var command = new CreateManualFundOrderCommand(request) with { CommandId = Guid.NewGuid() };
        Assert.True(command.Compute(state, Now, "admin", 7001, out var fundChange));
        Assert.Empty(state.Orders);
        Assert.Equal(2, state.Revision);
        Assert.Null(state.PendingEvent);
        var reserved = command.CreateFundCompositionReservedEvent(fundChange);
        Assert.True(state.Update(reserved, command));
        Assert.Single(state.Orders);
        Assert.Equal(7001, state.Orders.Single().OrderId);
        var restored = new PortfolioFundAggregate();
        restored.Replay([createdFund, activatedFund, reserved]);
        Assert.Equal(state.Orders.Single(), restored.Orders.Single());
    }

    [Fact]
    public void Wrong_command_identity_is_rejected_before_revision_or_business_state_changes()
    {
        var state = new PortfolioAggregate();
        var command = new CreatePortfolioCommand(PortfolioAggregateTests.Draft(), Guid.NewGuid()) with { CommandId = Guid.NewGuid() };
        command.Compute(state, Now, "admin", out var portfolioChange);
        var created = command.CreatePortfolioCreatedEvent(portfolioChange) with { CommandId = Guid.NewGuid() };
        Assert.False(state.Update(created, command));
        Assert.Null(state.Current);
        Assert.Equal(0, state.Revision);
        Assert.Null(state.PendingEvent);
    }

    [Fact]
    public void Policy_computation_does_not_activate_before_event_application()
    {
        var state = new PortfolioFinancialPolicyAggregate();
        state.Create(Guid.NewGuid(), Guid.NewGuid(), PortfolioFinancialPolicyAggregateTests.ValidPolicy() with { EffectiveFromUtc = Now.AddMinutes(-1) }, Now, "admin");
        var command = new ActivateAndAssignPortfolioFinancialPolicyCommand
        {
            CommandId = Guid.NewGuid(), ExpectedPolicyRevision = state.Revision, PolicyVersion = 1
        };
        Assert.True(command.Compute(state, Now, "admin", out var financialPolicyChange), financialPolicyChange.RejectionReason);
        Assert.Equal(PortfolioFinancialPolicyState.Draft, state.Current!.OperatingState);
        Assert.Equal(1, state.Revision);
        var activated = command.CreatePortfolioFinancialPolicyActivatedEvent(financialPolicyChange);
        Assert.True(state.Update(activated, command));
        Assert.Equal(PortfolioFinancialPolicyState.Active, state.Current.OperatingState);
    }

    public static IEnumerable<object[]> SourceEventFactories() => typeof(CreatePortfolio).Assembly.GetTypes()
        .Where(type => type.IsAbstract && type.IsSealed && type.Namespace?.Contains(".Command") == true)
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
        .Where(method => method.Name.StartsWith("Create", StringComparison.Ordinal)
            && typeof(IEvent).IsAssignableFrom(method.ReturnType)
            && method.GetParameters() is { Length: 2 } parameters
            && typeof(ICommand).IsAssignableFrom(parameters[0].ParameterType))
        .Select(method => new object[] { method.DeclaringType!.FullName!, method.Name });

    [Theory]
    [MemberData(nameof(SourceEventFactories))]
    public void Every_source_event_factory_preserves_command_identity_before_state_application(string typeName, string methodName)
    {
        var type = typeof(CreatePortfolio).Assembly.GetType(typeName)!;
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        var parameters = method.GetParameters();
        var command = (ICommand)Activator.CreateInstance(parameters[0].ParameterType)!;
        var commandId = Guid.NewGuid();
        parameters[0].ParameterType.GetProperty(nameof(ICommand.CommandId))!.SetValue(command, commandId);
        var computation = RuntimeHelpers.GetUninitializedObject(parameters[1].ParameterType);
        var created = (IEvent)method.Invoke(null, [command, computation])!;
        Assert.Equal(commandId, created.CommandId);
    }
}
