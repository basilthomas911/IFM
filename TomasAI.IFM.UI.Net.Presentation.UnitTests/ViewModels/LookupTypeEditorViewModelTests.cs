using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Reference;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class LookupTypeEditorViewModelTests
{
    [Fact]
    public async Task Load_StartsTerminalListenerBeforePublishingLookupState()
    {
        var lookup = Lookup("G2-Lookup", "A", "added");
        var subject = CreateSubject([lookup], ["G2-Lookup"]);
        var namesLoaded = 0;
        subject.ViewModel.OnLookupTypeNamesLoaded = () => namesLoaded++;

        await subject.ViewModel.LoadLookupTypes();

        subject.EventSource.IsStarted.Should().BeTrue();
        subject.ViewModel.LookupTypes.Values.Should().Equal(lookup);
        subject.ViewModel.LookupTypeNames.Should().Equal("G2-Lookup");
        subject.ViewModel.GetNextOrderId("G2-Lookup").Should().Be(1);
        subject.ViewModel.GetNextOrderId("new-partition").Should().Be(0);
        namesLoaded.Should().Be(1);
        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.EventSource.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task Add_WaitsForExactTerminalBeforeRefreshingAndCompletingUiCallback()
    {
        var commandId = Guid.NewGuid();
        var added = Lookup("G2-Lookup", "A", "added");
        var subject = CreateSubject([], [], [added], ["G2-Lookup"]);
        var callbackCount = 0;
        subject.CommandApi.AddLookupTypeAsync(added).Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadLookupTypes();

        var operation = subject.ViewModel.AddLookupType(added, () => callbackCount++);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new LookupTypeAddedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            LookupType = added
        });
        operation.IsCompleted.Should().BeFalse();
        callbackCount.Should().Be(0);
        await subject.EventSource.PublishAsync(new LookupTypeAddedCompleteEvent
        {
            CommandId = commandId,
            LookupType = added
        });
        await operation;

        callbackCount.Should().Be(1);
        subject.ViewModel.LookupTypes.Values.Should().Equal(added);
        subject.ViewModel.LookupTypeNames.Should().Equal("G2-Lookup");
        subject.ViewModel.LastStatusMessage.Should().Contain("Added");
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.QueryApi.Received(2).GetLookupTypesAsync();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Change_PropagatesExactTerminalFailureWithoutRefreshingOrCompletingUiCallback()
    {
        var commandId = Guid.NewGuid();
        var original = Lookup("G2-Lookup", "A", "added");
        var changed = original with { ShortCode = "B", Description = "changed" };
        var subject = CreateSubject([original], ["G2-Lookup"]);
        var callbackCount = 0;
        subject.CommandApi.ChangeLookupTypeAsync(original.Id, changed, true)
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadLookupTypes();

        var operation = subject.ViewModel.ChangeLookupType(
            original.Id,
            changed,
            true,
            () => callbackCount++);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new LookupTypeChangedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 7024,
            ErrorMessage = "durable lookup change failed"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<ModelOperationException>();
        exception.Which.ErrorCode.Should().Be(7024);
        callbackCount.Should().Be(0);
        subject.ViewModel.LookupTypes.Values.Should().Equal(original);
        await subject.QueryApi.Received(1).GetLookupTypesAsync();
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Remove_WaitsForTerminalThenRefreshesTheDurableCatalog()
    {
        var commandId = Guid.NewGuid();
        var removed = Lookup("G2-Lookup", "B", "changed");
        var subject = CreateSubject([removed], ["G2-Lookup"], [], []);
        subject.CommandApi.RemoveLookupTypeAsync(removed.Id, true)
            .Returns(new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadLookupTypes();

        var operation = subject.ViewModel.RemoveLookupType(removed.Id, true);
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new LookupTypeRemovedCompleteEvent
        {
            CommandId = commandId,
            LookupTypeId = removed.Id,
            EntityId = removed.Id
        });
        await operation;

        subject.ViewModel.LookupTypes.Should().BeEmpty();
        subject.ViewModel.LookupTypeNames.Should().BeEmpty();
        subject.ViewModel.LastStatusMessage.Should().Contain("Removed");
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.QueryApi.Received(2).GetLookupTypesAsync();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CompletionBeforeCommandResponse_IsBufferedAndCorrelated()
    {
        var commandId = Guid.NewGuid();
        var added = Lookup("G2-Lookup", "A", "added");
        var subject = CreateSubject([], [], [added], ["G2-Lookup"]);
        subject.CommandApi.AddLookupTypeAsync(added).Returns(_ => PublishEarlyAsync());
        await subject.ViewModel.LoadLookupTypes();

        await subject.ViewModel.AddLookupType(added, () => { });

        subject.ViewModel.LookupTypes.Values.Should().Equal(added);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);

        async Task<ServiceResult<Guid>> PublishEarlyAsync()
        {
            await subject.EventSource.PublishAsync(new LookupTypeAddedCompleteEvent
            {
                CommandId = commandId,
                LookupType = added
            });
            return new ServiceOk<Guid>(commandId);
        }
    }

    static Subject CreateSubject(
        LookupTypeReadModel[] initialTypes,
        string[] initialNames,
        LookupTypeReadModel[]? refreshedTypes = null,
        string[]? refreshedNames = null)
    {
        var queryApi = Substitute.For<IReferenceQueryApi>();
        queryApi.GetLookupTypesAsync().Returns(
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([.. initialTypes])),
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([.. refreshedTypes ?? initialTypes])));
        queryApi.GetLookupTypeNamesAsync().Returns(
            new ServiceOk<string[]>(initialNames),
            new ServiceOk<string[]>(refreshedNames ?? initialNames));
        var commandApi = Substitute.For<IReferenceCommandApi>();
        var eventConsumer = Substitute.For<ILookupTypeUIEventConsumer>();
        var eventSource = new TestLookupTypeEventSource(eventConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<ReferenceQueryModel>().Returns(new ReferenceQueryModel(queryApi));
        appRoot.GetModel<ReferenceCommandModel>().Returns(new ReferenceCommandModel(commandApi));
        appRoot.GetModel<LookupTypeEventModel>().Returns(new LookupTypeEventModel(eventConsumer));
        return new Subject(
            new LookupTypeEditorViewModel(appRoot),
            queryApi,
            commandApi,
            eventSource);
    }

    static LookupTypeReadModel Lookup(string name, string shortCode, string description)
        => new(name, shortCode, 0, description, DateTime.UtcNow, "test");

    static async Task WaitForCommandAsync(LookupTypeEditorViewModel viewModel, Guid commandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != commandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(commandId);
    }

    sealed record Subject(
        LookupTypeEditorViewModel ViewModel,
        IReferenceQueryApi QueryApi,
        IReferenceCommandApi CommandApi,
        TestLookupTypeEventSource EventSource);

    sealed class TestLookupTypeEventSource
    {
        Func<IEvent, ValueTask>? _listener;

        public TestLookupTypeEventSource(ILookupTypeUIEventConsumer consumer)
        {
            consumer.StartAsync(Arg.Any<Func<IEvent, ValueTask>>()).Returns(call =>
            {
                _listener = call.Arg<Func<IEvent, ValueTask>>();
                IsStarted = true;
                return ValueTask.CompletedTask;
            });
            consumer.StopAsync().Returns(_ =>
            {
                IsStarted = false;
                return ValueTask.CompletedTask;
            });
        }

        public bool IsStarted { get; private set; }

        public ValueTask PublishAsync(IEvent terminalEvent)
            => _listener?.Invoke(terminalEvent)
                ?? throw new InvalidOperationException("The lookup terminal-event listener has not started.");
    }
}
