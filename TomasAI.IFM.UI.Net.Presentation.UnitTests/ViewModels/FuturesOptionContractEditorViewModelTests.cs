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

public class FuturesOptionContractEditorViewModelTests
{
    [Fact]
    public async Task LoadOperation_StartsListenerAndPublishesCompleteSnapshot()
    {
        var contract = CreateContract("ES20260918C5000");
        var subject = CreateSubject([contract]);

        await subject.ViewModel.LoadOperation.ExecuteAsync();

        subject.EventSource.IsStarted.Should().BeTrue();
        subject.ViewModel.AllLookupTypesLoaded.Should().BeTrue();
        subject.ViewModel.SelectedSymbol.Should().Be("ES");
        subject.ViewModel.OptionTypes.Should().ContainSingle();
        subject.ViewModel.FuturesOptionContracts.Should().Equal(contract);
        subject.ViewModel.GetFuturesOptionContract(-1).Should().BeNull();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddOperation_IgnoresUnrelatedEventAndAwaitsCorrelatedCompletion()
    {
        var commandId = Guid.NewGuid();
        var existing = CreateContract("ES20260918C5000");
        var added = CreateContract("ES20260918P4500", "Put", 4500);
        var subject = CreateSubject([existing]);
        subject.QueryApi.GetFuturesOptionContractsAsync("ES").Returns(
            new ServiceOk<FuturesOptionContractReadModel[]>([existing]),
            new ServiceOk<FuturesOptionContractReadModel[]>([existing, added]));
        subject.CommandApi.AddFuturesOptionContractAsync(added, true).Returns(
            new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareAdd(added);

        var operation = subject.ViewModel.AddOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new FuturesOptionContractAddedCompleteEvent
        {
            CommandId = Guid.NewGuid(),
            Contract = added
        });
        operation.IsCompleted.Should().BeFalse("an unrelated terminal event must be ignored");

        await subject.EventSource.PublishAsync(new FuturesOptionContractAddedCompleteEvent
        {
            CommandId = commandId,
            Contract = added
        });
        await operation;

        subject.ViewModel.CommandId.Should().BeEmpty();
        subject.ViewModel.FuturesOptionContracts.Should().Equal(existing, added);
        subject.ViewModel.LastStatusMessage.Should().Contain(added.ContractId);
        subject.ViewModel.AddOperation.CanExecute.Should().BeFalse();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RemoveOperation_PreservesCodedTerminalFailure()
    {
        var commandId = Guid.NewGuid();
        var contract = CreateContract("ES20260918C5000");
        var subject = CreateSubject([contract]);
        subject.CommandApi.RemoveFuturesOptionContractAsync(contract.ContractId, true).Returns(
            new ServiceOk<Guid>(commandId));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareRemove(contract);

        var operation = subject.ViewModel.RemoveOperation.ExecuteAsync();
        await WaitForCommandAsync(subject.ViewModel, commandId);
        await subject.EventSource.PublishAsync(new FuturesOptionContractRemovedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 714,
            ErrorMessage = "option contract is in use"
        });

        var exception = await FluentActions.Awaiting(() => operation)
            .Should().ThrowAsync<ModelOperationException>();
        exception.Which.ErrorCode.Should().Be(714);
        subject.ViewModel.RemoveOperation.LastFailure.Should().BeSameAs(exception.Which);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CompletionBeforeCommandResponse_IsBufferedAndCorrelated()
    {
        var commandId = Guid.NewGuid();
        var existing = CreateContract("ES20260918C5000");
        var added = CreateContract("ES20260918P4500", "Put", 4500);
        var subject = CreateSubject([existing]);
        subject.QueryApi.GetFuturesOptionContractsAsync("ES").Returns(
            new ServiceOk<FuturesOptionContractReadModel[]>([existing]),
            new ServiceOk<FuturesOptionContractReadModel[]>([existing, added]));
        subject.CommandApi.AddFuturesOptionContractAsync(added, true).Returns(
            _ => PublishEarlyCompletionAsync());
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.PrepareAdd(added);

        await subject.ViewModel.AddOperation.ExecuteAsync();

        subject.ViewModel.FuturesOptionContracts.Should().Equal(existing, added);
        subject.ViewModel.CommandId.Should().BeEmpty();
        await subject.ViewModel.StopAsync(CancellationToken.None);

        async Task<ServiceResult<Guid>> PublishEarlyCompletionAsync()
        {
            await subject.EventSource.PublishAsync(new FuturesOptionContractAddedCompleteEvent
            {
                CommandId = commandId,
                Contract = added
            });
            return new ServiceOk<Guid>(commandId);
        }
    }

    [Fact]
    public void ViewModel_DeclaresObservableStateWithoutViewCallbacks()
    {
        typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(FuturesOptionContractEditorViewModel))
            .Should().BeTrue();

        var callbacks = typeof(FuturesOptionContractEditorViewModel)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member switch
            {
                FieldInfo field => typeof(Delegate).IsAssignableFrom(field.FieldType),
                PropertyInfo property => typeof(Delegate).IsAssignableFrom(property.PropertyType),
                _ => false
            });

        callbacks.Should().BeEmpty();
    }

    static Subject CreateSubject(FuturesOptionContractReadModel[] contracts)
    {
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        ConfigureLookup(referenceApi, "SecurityType", "FOP");
        ConfigureLookup(referenceApi, "Currency", "USD");
        ConfigureLookup(referenceApi, "Exchange", "CME");
        ConfigureLookup(referenceApi, "Multiplier", "50");
        ConfigureLookup(referenceApi, "OptionType", "Call");
        ConfigureLookup(referenceApi, "Symbol", "ES");

        var queryApi = Substitute.For<IMarketDataQueryApi>();
        queryApi.GetFuturesOptionContractsAsync("ES").Returns(
            new ServiceOk<FuturesOptionContractReadModel[]>(contracts));
        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
        var eventConsumer = Substitute.For<IMarketDataUIEventConsumer>();
        var eventSource = new TestMarketDataEventSource(eventConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<ReferenceQueryModel>().Returns(new ReferenceQueryModel(referenceApi));
        appRoot.GetModel<MarketDataQueryModel>().Returns(new MarketDataQueryModel(queryApi, feedQueryApi));
        appRoot.GetModel<MarketDataCommandModel>().Returns(new MarketDataCommandModel(commandApi));
        appRoot.GetModel<MarketDataEventModel>().Returns(new MarketDataEventModel(eventConsumer));

        return new Subject(
            new FuturesOptionContractEditorViewModel(appRoot),
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

    static FuturesOptionContractReadModel CreateContract(
        string contractId,
        string optionType = "Call",
        double strikePrice = 5000)
        => new(
            contractId,
            $"{contractId} contract",
            "ES",
            contractId,
            "FOP",
            "USD",
            "CME",
            "50",
            new DateOnly(2026, 9, 18),
            strikePrice,
            optionType);

    static async Task WaitForCommandAsync(
        FuturesOptionContractEditorViewModel viewModel,
        Guid expectedCommandId)
    {
        for (var attempt = 0; attempt < 100 && viewModel.CommandId != expectedCommandId; attempt++)
            await Task.Delay(5);
        viewModel.CommandId.Should().Be(expectedCommandId);
    }

    sealed record Subject(
        FuturesOptionContractEditorViewModel ViewModel,
        IMarketDataQueryApi QueryApi,
        IMarketDataCommandApi CommandApi,
        TestMarketDataEventSource EventSource);

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
