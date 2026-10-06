using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests;

public sealed class SecuritiesCommandConventionTests
{
    public static IEnumerable<object[]> Factories()
    {
        var futures = SampleData.FuturesContract;
        var option = SampleData.FuturesOptionContract1;
        yield return [typeof(AddFuturesContract), new AddFuturesContractCommand(futures)];
        yield return [typeof(ChangeFuturesContract), new ChangeFuturesContractCommand(futures.Id, futures)];
        yield return [typeof(RemoveFuturesContract), new RemoveFuturesContractCommand(futures.Id)];
        yield return [typeof(AddFuturesOptionContract), new AddFuturesOptionContractCommand(option)];
        yield return [typeof(AddFuturesOptionContracts), new AddFuturesOptionContractsCommand([option])];
        yield return [typeof(ChangeFuturesOptionContract), new ChangeFuturesOptionContractCommand(option.ContractId, option)];
        yield return [typeof(RemoveFuturesOptionContract), new RemoveFuturesOptionContractCommand(option.ContractId)];
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factory_preserves_command_identity_before_state_application(Type handler, ICommand command)
    {
        var commandId = Guid.NewGuid();
        command.GetType().GetProperty("CommandId")!.SetValue(command, commandId);
        var compute = handler.GetMethod("Compute", BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] inputs = compute.GetParameters().Length == 2 ? [command, null] : [command, false, null];
        compute.Invoke(null, inputs).Should().Be(true);
        var factory = handler.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => method.Name.StartsWith("Create", StringComparison.Ordinal));
        var sourceEvent = (IEvent)factory.Invoke(null, [command, inputs[^1]])!;
        sourceEvent.CommandId.Should().Be(commandId);
    }

    [Fact]
    public void Futures_replay_restores_contract_identity_and_rejections_leave_state_unchanged()
    {
        var contract = SampleData.FuturesContract;
        var amended = contract with { ContractId = "ES20261218" };
        var state = new FuturesContractCommandState();
        new AddFuturesContractCommand(contract) { CommandId = Guid.NewGuid(), Subject = Subject("Add", contract.Id.Format()) }.Execute(state).Success.Should().BeTrue();
        new ChangeFuturesContractCommand(contract.Id, amended) { CommandId = Guid.NewGuid(), Subject = Subject("Change", amended.Id.Format()) }.Execute(state).Success.Should().BeTrue();
        var replayed = new FuturesContractCommandState();
        replayed.ReplayEvents(state.Events.ToArray());
        replayed.FuturesContractExists(contract.Id).Should().BeFalse();
        replayed.FuturesContractExists(amended.Id).Should().BeTrue();
        var missing = new RemoveFuturesContractCommand(contract.Id) { CommandId = Guid.NewGuid(), Subject = Subject("Remove", contract.Id.Format()) };
        missing.Execute(replayed).Success.Should().BeFalse();
        replayed.Events.Should().BeEmpty();
        replayed.FuturesContractExists(amended.Id).Should().BeTrue();
        new RemoveFuturesContractCommand(amended.Id) { CommandId = Guid.NewGuid(), Subject = Subject("Remove", amended.Id.Format()) }.Execute(replayed).Success.Should().BeTrue();
        replayed.FuturesContractExists(amended.Id).Should().BeFalse();
    }

    [Fact]
    public void Option_replay_retains_duplicate_guard_and_removal_updates_owned_state()
    {
        var option = SampleData.FuturesOptionContract1;
        var state = new FuturesOptionContractCommandState();
        var add = new AddFuturesOptionContractCommand(option) { CommandId = Guid.NewGuid(), Subject = Subject("Add", option.ContractId) };
        add.Execute(state).Success.Should().BeTrue();
        var replayed = new FuturesOptionContractCommandState();
        replayed.ReplayEvents(state.Events.ToArray());
        add.Execute(replayed).Success.Should().BeFalse();
        replayed.Events.Should().BeEmpty();
        new RemoveFuturesOptionContractCommand(option.ContractId) { CommandId = Guid.NewGuid(), Subject = Subject("Remove", option.ContractId) }.Execute(replayed).Success.Should().BeTrue();
        add.Execute(replayed).Success.Should().BeTrue();
    }

    private static ActorSubject Subject(string verb, string entityId) => new(ActorType.Command, "SecuritiesConventionTest", verb, entityId);
}
