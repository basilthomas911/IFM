using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
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
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.Symbol == "ES" && r.ContinuationToken == null), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<FuturesOptionContractPageReadModel>(new([existing], null)),
            new ServiceOk<FuturesOptionContractPageReadModel>(new([existing, added], null)));
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
            .Should().ThrowAsync<UiServiceOperationException>();
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
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.Symbol == "ES" && r.ContinuationToken == null), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<FuturesOptionContractPageReadModel>(new([existing], null)),
            new ServiceOk<FuturesOptionContractPageReadModel>(new([existing, added], null)));
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

    [Fact]
    public async Task Paging_loads_only_first_page_then_appends_once_and_stops_at_end()
    {
        var first = CreateContract("ES20260918C5000");
        var last = CreateContract("ES20260918C5001");
        var subject = CreateSubject([first]);
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call => new ServiceOk<FuturesOptionContractPageReadModel>(
                call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is null
                    ? new([first], "next") : new([last], null)));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.FuturesOptionContracts.Should().Equal(first);
        subject.ViewModel.HasMoreContracts.Should().BeTrue();
        await subject.QueryApi.Received(1).GetFuturesOptionContractsPageAsync(
            Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.PageSize == 200 && r.ContinuationToken == null), Arg.Any<CancellationToken>());
        await subject.QueryApi.DidNotReceive().GetFuturesOptionContractsAsync(Arg.Any<string>());
        await subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        subject.ViewModel.FuturesOptionContracts.Should().Equal(first, last);
        subject.ViewModel.HasMoreContracts.Should().BeFalse();
        await subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        await subject.QueryApi.Received(2).GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>());
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Concurrent_scroll_requests_share_one_page_request()
    {
        var subject = CreateSubject([]);
        var pending = new TaskCompletionSource<ServiceResult<FuturesOptionContractPageReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is null
                ? Task.FromResult<ServiceResult<FuturesOptionContractPageReadModel>>(new ServiceOk<FuturesOptionContractPageReadModel>(new([CreateContract("ES20260918C5000")], "next")))
                : pending.Task);
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        var first = subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        var duplicate = subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        pending.SetResult(new ServiceOk<FuturesOptionContractPageReadModel>(new([CreateContract("ES20260918C5001")], null)));
        await Task.WhenAll(first, duplicate);
        subject.ViewModel.FuturesOptionContracts.Should().HaveCount(2);
        await subject.QueryApi.Received(1).GetFuturesOptionContractsPageAsync(
            Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.ContinuationToken == "next"), Arg.Any<CancellationToken>());
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Refresh_discards_an_inflight_page_from_the_old_sequence()
    {
        var subject = CreateSubject([]);
        var pending = new TaskCompletionSource<ServiceResult<FuturesOptionContractPageReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fresh = CreateContract("ES20260918C5002");
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is null
                ? Task.FromResult<ServiceResult<FuturesOptionContractPageReadModel>>(new ServiceOk<FuturesOptionContractPageReadModel>(new([fresh], "next")))
                : pending.Task);
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        var stale = subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        await subject.ViewModel.LoadContractsOperation.ExecuteAsync();
        pending.SetResult(new ServiceOk<FuturesOptionContractPageReadModel>(new([CreateContract("ES20260918C5003")], null)));
        try { await stale; } catch (OperationCanceledException) { }
        subject.ViewModel.FuturesOptionContracts.Should().Equal(fresh);
        subject.ViewModel.HasMoreContracts.Should().BeTrue();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Expired_cursor_restarts_from_first_page_instead_of_combining_sequences()
    {
        var subject = CreateSubject([]);
        var fresh = CreateContract("ES20260918C5002");
        var requests = 0;
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ++requests;
                return call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is not null
                    ? new ServiceFailed<FuturesOptionContractPageReadModel>(1033, "Expired token")
                    : new ServiceOk<FuturesOptionContractPageReadModel>(requests == 1
                        ? new([CreateContract("ES20260918C5004")], "next") : new([fresh], null));
            });
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        await subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        subject.ViewModel.FuturesOptionContracts.Should().Equal(fresh);
        subject.ViewModel.HasMoreContracts.Should().BeFalse();
        requests.Should().Be(3);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Repeated_page_failure_stops_automatic_restarts_and_retains_cached_rows()
    {
        var first = CreateContract("ES20260918C5000");
        var subject = CreateSubject([first]);
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is null
                ? new ServiceOk<FuturesOptionContractPageReadModel>(new([first], "next"))
                : new ServiceFailed<FuturesOptionContractPageReadModel>(1033, "Cannot resume"));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        await subject.ViewModel.LoadMoreOperation.ExecuteAsync();
        await Assert.ThrowsAsync<UiServiceOperationException>(() => subject.ViewModel.LoadMoreOperation.ExecuteAsync());
        subject.ViewModel.FuturesOptionContracts.Should().Equal(first);
        await subject.QueryApi.Received(2).GetFuturesOptionContractsPageAsync(
            Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.ContinuationToken == null), Arg.Any<CancellationToken>());
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Rapid_symbol_changes_publish_only_the_latest_symbol()
    {
        var es = CreateContract("ES20260918C5000");
        var nq = CreateContract("NQ20260918C5000") with { Symbol = "NQ" };
        var subject = CreateSubject([es], multipleSymbols: true);
        var pending = new TaskCompletionSource<ServiceResult<FuturesOptionContractPageReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        subject.QueryApi.GetFuturesOptionContractsPageAsync(
            Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.Symbol == "NQ"), Arg.Any<CancellationToken>()).Returns(pending.Task);
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        subject.ViewModel.SelectSymbol(1);
        var load = subject.ViewModel.LoadContractsOperation.ExecuteAsync();
        subject.ViewModel.SelectSymbol(0);
        pending.SetResult(new ServiceOk<FuturesOptionContractPageReadModel>(new([nq], null)));
        await load;
        subject.ViewModel.SelectedSymbol.Should().Be("ES");
        subject.ViewModel.FuturesOptionContracts.Should().Equal(es);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Saved_contract_selection_can_be_restored_from_a_later_page()
    {
        var first = CreateContract("ES20260918C5000");
        var saved = CreateContract("ES20260918C5001");
        var subject = CreateSubject([first]);
        subject.QueryApi.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call => new ServiceOk<FuturesOptionContractPageReadModel>(
                call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is null
                    ? new([first], "next") : new([saved], null)));
        await subject.ViewModel.LoadOperation.ExecuteAsync();
        await subject.ViewModel.EnsureContractLoadedAsync(saved.ContractId);
        subject.ViewModel.FuturesOptionContracts.Should().Equal(first, saved);
        await subject.QueryApi.Received(2).GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>());
        await subject.ViewModel.DisposeAsync();
    }

    static Subject CreateSubject(FuturesOptionContractReadModel[] contracts, bool multipleSymbols = false)
    {
        var referenceApi = Substitute.For<IReferenceQueryApi>();
        ConfigureLookup(referenceApi, "SecurityType", "FOP");
        ConfigureLookup(referenceApi, "Currency", "USD");
        ConfigureLookup(referenceApi, "Exchange", "CME");
        ConfigureLookup(referenceApi, "Multiplier", "50");
        ConfigureLookup(referenceApi, "OptionType", "Call");
        ConfigureLookup(referenceApi, "Symbol", "ES");
        if (multipleSymbols)
            referenceApi.GetLookupTypesAsync("Symbol").Returns(new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([
                new LookupTypeReadModel("Symbol", "ES", 0, "ES", DateTime.UtcNow, "test"),
                new LookupTypeReadModel("Symbol", "NQ", 1, "NQ", DateTime.UtcNow, "test")])));

        var queryApi = Substitute.For<IMarketDataQueryApi>();
        queryApi.GetFuturesOptionContractsPageAsync(Arg.Is<GetFuturesOptionContractsPageParameter>(r => r.Symbol == "ES" && r.ContinuationToken == null), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<FuturesOptionContractPageReadModel>(new(contracts, null)));
        var commandApi = Substitute.For<IMarketDataCommandApi>();
        var feedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
        var eventConsumer = Substitute.For<IMarketDataUIEventConsumer>();
        var eventSource = new TestMarketDataEventSource(eventConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.MarketDataQueries.Returns(new MarketDataQueryService(queryApi, feedQueryApi));
        appRoot.Services.MarketDataCommands.Returns(new MarketDataCommandService(commandApi));
        appRoot.Services.MarketDataEvents.Returns(new MarketDataEventService(eventConsumer));

        return new Subject(
            new FuturesOptionContractEditorViewModel(appRoot, UiServiceFactory.CreateReference(referenceApi)),
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
