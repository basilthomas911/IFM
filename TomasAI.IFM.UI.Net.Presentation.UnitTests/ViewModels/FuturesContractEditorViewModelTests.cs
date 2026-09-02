using FluentAssertions;
using NSubstitute;
using System.ComponentModel;
using System.Reflection;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class FuturesContractEditorViewModelTests
{
    [Fact]
    public async Task LoadOperation_PublishesOneCompleteEditorSnapshot()
    {
        var contract = CreateContract("ES20260918");
        var subject = CreateSubject([contract]);

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.AllLookupTypesLoaded.Should().BeTrue();
        subject.ViewModel.SecurityTypes.Should().ContainSingle();
        subject.ViewModel.Currencies.Should().ContainSingle();
        subject.ViewModel.Exchanges.Should().ContainSingle();
        subject.ViewModel.Multipliers.Should().ContainSingle();
        subject.ViewModel.Symbols.Should().ContainSingle();
        subject.ViewModel.OnTheRun.Should().Equal("Yes", "No");
        subject.ViewModel.FuturesContracts.Should().Equal(contract);
        subject.ViewModel.GetFuturesContract(-1).Should().BeNull();
        subject.ViewModel.GetContractMonth(12).Should().Be("Z");
        subject.EventSource.IsStarted.Should().BeTrue();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddOperation_IsGuardedAndRefreshesTheContractSnapshot()
    {
        var existing = CreateContract("ES20260918");
        var added = CreateContract("ES20261218");
        var subject = CreateSubject([existing]);
        subject.QueryApi.GetFuturesContractsAsync().Returns(
            new ServiceOk<FuturesContractV3ReadModel[]>([existing]),
            new ServiceOk<FuturesContractV3ReadModel[]>([existing, added]));
        var commandId = Guid.NewGuid();
        subject.CommandApi.AddFuturesContractAsync(added, true).Returns(
            new ServiceOk<Guid>(commandId));

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.ViewModel.AddOperation.CanExecute.Should().BeFalse();
        subject.ViewModel.PrepareAdd(added);
        subject.ViewModel.AddOperation.CanExecute.Should().BeTrue();

        var operation = subject.ViewModel.AddOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new FuturesContractAddedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            Contract = added
        });
        operation.IsCompleted.Should().BeFalse("an unrelated terminal event must be ignored");
        await subject.EventSource.PublishAsync(new FuturesContractAddedCompleteEvent
        {
            CommandId = commandId,
            Contract = added
        });
        await operation;

        subject.ViewModel.FuturesContracts.Should().Equal(existing, added);
        subject.ViewModel.LastStatusMessage.Should().Contain(added.ContractId);
        subject.ViewModel.AddOperation.CanExecute.Should().BeFalse();
        await subject.CommandApi.Received(1).AddFuturesContractAsync(added, true);
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RemoveOperation_PreservesCodedTerminalFailure()
    {
        var commandId = Guid.NewGuid();
        var contract = CreateContract("ES20260918");
        var subject = CreateSubject([contract]);
        subject.CommandApi.RemoveFuturesContractAsync(contract.Id, true).Returns(
            new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareRemove(contract.Id);

        var operation = subject.ViewModel.RemoveOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new FuturesContractRemovedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 714,
            ErrorMessage = "futures contract is in use"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<UiServiceOperationException>();
        exception.Which.ErrorCode.Should().Be(714);
        subject.ViewModel.RemoveOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LoadOperation_PreservesCodedReferenceFailure()
    {
        var subject = CreateSubject([]);
        subject.ReferenceApi.GetLookupTypesAsync("SecurityType").Returns(
            new ServiceFailed<LookupTypeCollection>(611, "security types unavailable"));

        var exception = await FluentActions.Awaiting(
                () => subject.ViewModel.LoadOperation.ExecuteAsync())
            .Should().ThrowAsync<UiOperationException>();

        exception.Which.ErrorCode.Should().Be(611);
        subject.ViewModel.LoadOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.AllLookupTypesLoaded.Should().BeFalse();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void ViewModel_DeclaresObservableStateWithoutViewCallbackDelegates()
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(FuturesContractEditorViewModel))
            .Should().BeTrue();

        var callbacks = typeof(FuturesContractEditorViewModel)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            });

        callbacks.Should().BeEmpty();
    }

    static Subject CreateSubject(FuturesContractV3ReadModel[] contracts)
    {
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        ConfigureLookup(referenceApi, "SecurityType", "FUT");
        ConfigureLookup(referenceApi, "Currency", "USD");
        ConfigureLookup(referenceApi, "Exchange", "CME");
        ConfigureLookup(referenceApi, "Multiplier", "50");
        ConfigureLookup(referenceApi, "Symbol", "ES");

        var queryApi = Substitute.For<IMarketDataQueryApi>();
        queryApi.GetFuturesContractsAsync().Returns(
            new ServiceOk<FuturesContractV3ReadModel[]>(contracts));
        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
        var eventConsumer = Substitute.For<IMarketDataUIEventConsumer>();
        var eventSource = new TestMarketDataEventSource(eventConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.MarketDataQueries.Returns(new MarketDataQueryService(queryApi, feedQueryApi));
        appRoot.Services.MarketDataCommands.Returns(new MarketDataCommandService(commandApi));
        appRoot.Services.MarketDataEvents.Returns(new MarketDataEventService(eventConsumer));

        return new Subject(
            new FuturesContractEditorViewModel(appRoot, UiServiceFactory.CreateReference(referenceApi)),
            referenceApi,
            queryApi,
            commandApi,
            eventSource);
    }

    static void ConfigureLookup(IReferenceQueryApi api, string name, string shortCode)
    {
        var lookup = new LookupTypeReadModel(
            name, shortCode, 0, shortCode, DateTime.UtcNow, "test");
        api.GetLookupTypesAsync(name).Returns(
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([lookup])));
    }

    static FuturesContractV3ReadModel CreateContract(string contractId)
        => new(
            contractId,
            $"{contractId} contract",
            "ES",
            contractId,
            "FUT",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 9, 18),
            true);

    sealed record Subject(
        FuturesContractEditorViewModel ViewModel,
        IReferenceQueryApi ReferenceApi,
        IMarketDataQueryApi QueryApi,
        IMarketDataCommandApi CommandApi,
        TestMarketDataEventSource EventSource);

    static async Task WaitForCommandAsync(
        FuturesContractEditorViewModel viewModel,
        Guid expectedCommandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != expectedCommandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(expectedCommandId);
    }

    sealed class TestMarketDataEventSource
    {
        Func<IEvent, ValueTask>? _listener;

        public TestMarketDataEventSource(IMarketDataUIEventConsumer consumer)
        {
            consumer.StartAsync(Arg.Any<ICollection<IEvent>>(), Arg.Any<Func<IEvent, ValueTask>>())
                .Returns(call =>
                {
                    _listener = call.ArgAt<Func<IEvent, ValueTask>>(1);
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

        public ValueTask PublishAsync(IEvent @event)
            => _listener?.Invoke(@event)
                ?? throw new InvalidOperationException("The event listener has not started.");
    }
}
