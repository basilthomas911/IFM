using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Fund;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public class AdjustFundTransactionReadModelTests
{
    [Fact]
    public async Task MatchingCompletionEvent_CompletesSubmittedAdjustment()
    {
        var commandId = Guid.NewGuid();
        var (viewModel, eventSource) = CreateSubject(commandId);
        await viewModel.StartListener();
        viewModel.SetPendingAdjustment(viewModel.GetAdjustmentTransaction(10m, "correction"));

        await viewModel.SubmitAdjustmentOperation.ExecuteAsync();
        await eventSource.PublishAsync(new OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent
        {
            CommandId = Guid.NewGuid()
        });

        viewModel.IsAdjustmentCompleted.Should().BeFalse("unrelated events must be ignored");
        await eventSource.PublishAsync(new OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent
        {
            CommandId = commandId
        });

        viewModel.IsAdjustmentCompleted.Should().BeTrue();
        viewModel.CommandId.Should().BeEmpty();
        viewModel.SubmitAdjustmentOperation.CanExecute.Should().BeTrue();
        await viewModel.StopListener();
    }

    [Fact]
    public async Task MatchingFailureEvent_PublishesCodedFailure()
    {
        var commandId = Guid.NewGuid();
        var (viewModel, eventSource) = CreateSubject(commandId);
        await viewModel.StartListener();
        viewModel.SetPendingAdjustment(viewModel.GetAdjustmentTransaction(10m, "correction"));
        await viewModel.SubmitAdjustmentOperation.ExecuteAsync();

        await eventSource.PublishAsync(new OpeningTradeFundTransactionAdjustmentCreatedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 811,
            ErrorMessage = "adjustment rejected"
        });

        viewModel.IsAdjustmentCompleted.Should().BeFalse();
        viewModel.AdjustmentFailure.Should().NotBeNull();
        viewModel.AdjustmentFailure!.ErrorCode.Should().Be(811);
        viewModel.AdjustmentFailure.Message.Should().Be("adjustment rejected");
        viewModel.CommandId.Should().BeEmpty();
        await viewModel.StopListener();
    }

    [Fact]
    public async Task CompletionArrivingBeforeCommandResponse_IsBufferedAndCorrelated()
    {
        var commandId = Guid.NewGuid();
        var (viewModel, eventSource, commandApi) = CreateUnconfiguredSubject();
        commandApi.CreateFundTransactionAsync(Arg.Any<FundTransactionReadModel>()).Returns(
            _ => PublishEarlyCompletionAsync());
        await viewModel.StartListener();
        viewModel.SetPendingAdjustment(viewModel.GetAdjustmentTransaction(10m, "correction"));

        await viewModel.SubmitAdjustmentOperation.ExecuteAsync();

        viewModel.IsAdjustmentCompleted.Should().BeTrue();
        viewModel.CommandId.Should().BeEmpty();
        await viewModel.StopListener();

        async Task<ServiceResult<Guid>> PublishEarlyCompletionAsync()
        {
            await eventSource.PublishAsync(new OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent
            {
                CommandId = commandId
            });
            return new ServiceOk<Guid>(commandId);
        }
    }

    static (AdjustFundTransactionReadModel ViewModel, TestFundEventSource EventSource) CreateSubject(Guid commandId)
    {
        var (viewModel, eventSource, commandApi) = CreateUnconfiguredSubject();
        commandApi.CreateFundTransactionAsync(Arg.Any<FundTransactionReadModel>()).Returns(
            Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId)));
        return (viewModel, eventSource);
    }

    static (AdjustFundTransactionReadModel ViewModel, TestFundEventSource EventSource, IFundCommandApi CommandApi)
        CreateUnconfiguredSubject()
    {
        var eventConsumer = Substitute.For<IFundUIEventConsumer>();
        var eventSource = new TestFundEventSource(eventConsumer);
        var commandApi = Substitute.For<IFundCommandApi>();
        var riskConsumer = Substitute.For<IFundRiskMarginUIEventConsumer>();
        var stateConsumer = Substitute.For<IFundOrderTradeStateUIEventConsumer>();
        var fundCommandModel = new FundCommandService(commandApi, riskConsumer, stateConsumer);
        var fundEventModel = new FundEventService(eventConsumer);
        var statusModel = new StatusConsoleService(
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<IStatusConsoleEventConsumer>());
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.FundCommands.Returns(fundCommandModel);
        appRoot.Services.FundEvents.Returns(fundEventModel);
        appRoot.Services.StatusConsole.Returns(statusModel);
        return (new AdjustFundTransactionReadModel(appRoot, OriginalTransaction(), 1000m), eventSource, commandApi);
    }

    static FundTransactionReadModel OriginalTransaction()
        => new(
            1,
            DateTime.UtcNow,
            FundTransactionType.OpeningTrade,
            7,
            10,
            20,
            default,
            new DateOnly(2026, 8, 11),
            default,
            "original",
            25m,
            1000m);

    sealed class TestFundEventSource
    {
        Func<IEvent, ValueTask>? _listener;

        public TestFundEventSource(IFundUIEventConsumer consumer)
        {
            consumer.StartAsync(Arg.Any<ICollection<IEvent>>(), Arg.Any<Func<IEvent, ValueTask>>())
                .Returns(call =>
                {
                    _listener = call.ArgAt<Func<IEvent, ValueTask>>(1);
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync().Returns(ValueTask.CompletedTask);
        }

        public ValueTask PublishAsync(IEvent @event)
            => _listener?.Invoke(@event)
                ?? throw new InvalidOperationException("The event listener has not started.");
    }
}
