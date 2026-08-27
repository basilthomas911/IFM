using System.Reflection;
using System.Collections;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

/// <summary>Qualifies the RD-10 Command actor dispatch and durable terminal reducer.</summary>
public sealed class RegimeDiscoveryCommandArchitectureTests
{
    /// <summary>Confirms the actor owns the mandatory parse, validation, and receive maps.</summary>
    [Fact]
    public void Command_actor_has_all_three_dispatch_maps()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var fields = typeof(RegimeDiscoveryCommandActor).GetFields(flags).Select(value => value.Name).ToArray();

        fields.Should().Contain(["_parseMap", "_validationMap", "_receiveMap"]);
        ReadMap("_parseMap").Contains(StartRegimeDiscoveryPipelineCommand.Verb).Should().BeTrue();
        ReadMap("_validationMap").Contains(nameof(StartRegimeDiscoveryPipelineCommand)).Should().BeTrue();
        ReadMap("_receiveMap").Contains(nameof(StartRegimeDiscoveryPipelineCommand)).Should().BeTrue();
    }

    /// <summary>Confirms replay reconstructs the complete successful terminal state.</summary>
    [Fact]
    public void Completed_event_reconstructs_terminal_state()
    {
        var input = RegimeDiscoveryCalculationModelTests.CreateInput();
        var state = new RegimeDiscoveryCommandState();
        var domainEvent = new RegimeDiscoveryCalculationCompletedEvent
        {
            EntityId = input.EntityId,
            WorkflowId = input.WorkflowId,
            InputWorkflowRevision = 7,
            CommandId = input.ResultId,
            EventId = 41,
            ParameterPayloadSha256 = new string('A', 64),
            SignalSnapshotId = input.Snapshot.SnapshotId,
            Result = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDiscoveryResult
            {
                ResultId = input.ResultId,
                WorkflowId = input.WorkflowId,
                EntityId = input.EntityId,
                SignalSnapshotId = input.Snapshot.SnapshotId
            },
            ResultPayloadSha256 = new string('B', 64)
        };

        state.Apply(domainEvent, addEvent: false).Should().BeTrue();

        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Completed);
        state.IsTerminal.Should().BeTrue();
        state.LastPersistedEventId.Should().Be(41);
        state.ResultPayloadSha256.Should().Be(new string('B', 64));
    }

    /// <summary>Confirms replay reconstructs the standard durable failure and structured reasons.</summary>
    [Fact]
    public void Failed_event_reconstructs_terminal_state()
    {
        var input = RegimeDiscoveryCalculationModelTests.CreateInput();
        var state = new RegimeDiscoveryCommandState();
        var domainEvent = new RegimeDiscoveryCalculationFailedEvent
        {
            EntityId = input.EntityId,
            WorkflowId = input.WorkflowId,
            InputWorkflowRevision = 7,
            CommandId = input.ResultId,
            EventId = 42,
            ParameterPayloadSha256 = new string('A', 64),
            SignalSnapshotId = input.Snapshot.SnapshotId,
            Failure = new() { ErrorCode = 23102, ErrorMessage = "missing required signal" },
            Reasons = [new() { Code = "RD.DATA.REQUIRED_MISSING" }]
        };

        state.Apply(domainEvent, addEvent: false).Should().BeTrue();

        state.Status.Should().Be(RegimeDiscoveryCommandStatus.Failed);
        state.Failure!.ErrorCode.Should().Be(23102);
        state.Reasons.Should().ContainSingle(value => value.Code == "RD.DATA.REQUIRED_MISSING");
    }

    static IDictionary ReadMap(string fieldName) => (IDictionary)typeof(RegimeDiscoveryCommandActor)
        .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
