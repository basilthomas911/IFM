using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests;

public sealed class CommandExtensionConventionTests
{
    [Fact]
    public void Computation_and_event_creation_leave_state_untouched_and_preserve_command_identity()
    {
        var command = Create();
        var state = new ParameterSetCommandState();
        command.Compute(state, ParameterMutationModel.RequestHash(command), out var change).Should().BeTrue();
        state.CatalogRevision.Should().Be(0);
        state.Versions.Should().BeEmpty();
        var sourceEvent = command.CreateParameterSetCreatedEvent(change);
        sourceEvent.CommandId.Should().Be(command.CommandId);
        state.Update(sourceEvent, command).Should().BeTrue();
        state.CatalogRevision.Should().Be(1);
        state.Versions[1].Should().BeEquivalentTo(change.ParameterVersion);
        state.Audit[command.CommandId].OperationId.Should().Be(command.CommandId);
    }

    [Fact]
    public async Task Revision_failure_returns_business_reason_without_applying_an_event()
    {
        var command = Create() with { ExpectedRevision = 9 };
        var state = new ParameterSetCommandState();
        var context = Substitute.For<IParameterSetCommandContext>();
        var result = await command.ExecuteAsync(context, state, Substitute.For<ILogger<ParameterSetCommandActor>>());
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("PARAM.REVISION_CONFLICT");
        state.CatalogRevision.Should().Be(0);
        state.Versions.Should().BeEmpty();
        state.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task Release_of_absent_startup_run_is_an_idempotent_no_op()
    {
        var state = new ParameterStartupCommandState();
        var command = new ReleaseSignalStartupPlanCommand { CommandId = Guid.NewGuid(), RunId = Guid.NewGuid() };
        var result = await command.ExecuteAsync(Substitute.For<IParameterStartupCommandContext>(), state, Substitute.For<ILogger<ParameterStartupCommandActor>>());
        result.Success.Should().BeTrue();
        state.Revision.Should().Be(0);
        state.ActiveRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_requires_a_draft_and_returns_failure_without_mutation()
    {
        var command = new PublishRegimeDiscoveryParameterSetCommand { CommandId = Guid.NewGuid() };
        var state = new RegimeDiscoveryConfigurationCommandState();
        var result = await command.ExecuteAsync(null!, state);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Draft");
        state.Status.Should().Be("Empty");
        state.ParameterSet.Should().BeNull();
    }

    [Fact]
    public void Every_source_event_factory_sets_the_originating_command_identity()
    {
        var factories = typeof(CreateParameterSet).Assembly.GetTypes()
            .Where(type => type.IsAbstract && type.IsSealed && type.Namespace?.EndsWith(".Command") == true)
            .SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic))
            .Where(method => method.Name.StartsWith("Create") && typeof(IEvent).IsAssignableFrom(method.ReturnType))
            .ToArray();
        factories.Should().HaveCount(16);
        foreach (var factory in factories)
        {
            var parameters = factory.GetParameters();
            var command = Activator.CreateInstance(parameters[0].ParameterType)!;
            var commandId = Guid.NewGuid();
            parameters[0].ParameterType.GetProperty("CommandId")!.SetValue(command, commandId);
            var entityProperty = parameters[0].ParameterType.GetProperty("EntityId")!;
            if (entityProperty.GetValue(command) is null)
                entityProperty.SetValue(command, Activator.CreateInstance(entityProperty.PropertyType));
            var change = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(parameters[1].ParameterType);
            var assignmentProperty = parameters[1].ParameterType.GetProperty("ParameterAssignment");
            if (assignmentProperty is not null)
                assignmentProperty.SetValue(change, System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ParameterAssignmentRevision)));
            var sourceEvent = (IEvent)factory.Invoke(null, [command, change])!;
            sourceEvent.CommandId.Should().Be(commandId, factory.DeclaringType!.Name);
        }
    }

    static CreateParameterSetCommand Create()
    {
        var setId = Guid.NewGuid();
        return new CreateParameterSetCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new(setId), Name = "Daily",
            PayloadJson = new RegimeDiscoveryParameterModel().CreateDraftPayload(setId)
        };
    }
}
