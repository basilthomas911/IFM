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

public sealed class FundCashTransactionViewModelTests
{
    [Theory]
    [InlineData(FundTransactionType.CashDeposit)]
    [InlineData(FundTransactionType.CashWithdrawal)]
    public void CreateTransaction_PreservesCashIntentAndRunReference(FundTransactionType transactionType)
    {
        var (viewModel, _, _) = CreateUnconfiguredSubject(transactionType);

        var transaction = viewModel.CreateTransaction(123.45m, " G2-RUN-Cash ");

        transaction.TransactionType.Should().Be(transactionType);
        transaction.FundId.Should().Be(7);
        transaction.ValueDate.Should().Be(new DateOnly(2026, 8, 18));
        transaction.Amount.Should().Be(123.45m);
        transaction.Description.Should().Be("G2-RUN-Cash");
        transaction.TransactionDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task MatchingCompletion_CompletesOnlyCorrelatedCashTransaction()
    {
        var commandId = Guid.NewGuid();
        var (viewModel, eventSource, commandApi) = CreateUnconfiguredSubject(FundTransactionType.CashDeposit);
        commandApi.CreateFundTransactionAsync(Arg.Any<FundTransactionReadModel>())
            .Returns(Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId)));
        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.SetPendingTransaction(viewModel.CreateTransaction(10m, "G2-deposit"));

        await viewModel.SubmitOperation.ExecuteAsync();
        await commandApi.Received(1).CreateFundTransactionAsync(Arg.Is<FundTransactionReadModel>(transaction =>
            transaction.FundId == 7
            && transaction.TransactionType == FundTransactionType.CashDeposit
            && transaction.Amount == 10m
            && transaction.Description == "G2-deposit"));
        await eventSource.PublishAsync(new FundTransactionCreatedCompleteEvent { CommandId = Guid.NewGuid() });
        viewModel.IsCompleted.Should().BeFalse();
        await eventSource.PublishAsync(new FundTransactionCreatedCompleteEvent { CommandId = commandId });

        viewModel.IsCompleted.Should().BeTrue();
        viewModel.CommandId.Should().BeEmpty();
        await viewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MatchingFailure_ExposesCodedFailureAndAllowsRetry()
    {
        var commandId = Guid.NewGuid();
        var (viewModel, eventSource, commandApi) = CreateUnconfiguredSubject(FundTransactionType.CashWithdrawal);
        commandApi.CreateFundTransactionAsync(Arg.Any<FundTransactionReadModel>())
            .Returns(Task.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId)));
        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.SetPendingTransaction(viewModel.CreateTransaction(10m, "G2-withdrawal"));
        await viewModel.SubmitOperation.ExecuteAsync();

        await eventSource.PublishAsync(new FundTransactionCreatedFailEvent
        {
            CommandId = commandId,
            ErrorCode = 812,
            ErrorMessage = "withdrawal rejected"
        });

        viewModel.IsCompleted.Should().BeFalse();
        viewModel.Failure.Should().NotBeNull();
        viewModel.Failure!.ErrorCode.Should().Be(812);
        viewModel.Failure.Message.Should().Be("withdrawal rejected");
        viewModel.CommandId.Should().BeEmpty();
        viewModel.SubmitOperation.CanExecute.Should().BeTrue();
        await viewModel.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CompletionBeforeCommandResponse_IsBufferedAndCorrelated()
    {
        var commandId = Guid.NewGuid();
        var (viewModel, eventSource, commandApi) = CreateUnconfiguredSubject(FundTransactionType.CashDeposit);
        commandApi.CreateFundTransactionAsync(Arg.Any<FundTransactionReadModel>()).Returns(
            _ => PublishEarlyCompletionAsync());
        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.SetPendingTransaction(viewModel.CreateTransaction(10m, "G2-deposit"));

        await viewModel.SubmitOperation.ExecuteAsync();

        viewModel.IsCompleted.Should().BeTrue();
        viewModel.CommandId.Should().BeEmpty();
        await viewModel.StopAsync(CancellationToken.None);

        async Task<ServiceResult<Guid>> PublishEarlyCompletionAsync()
        {
            await eventSource.PublishAsync(new FundTransactionCreatedCompleteEvent { CommandId = commandId });
            return new ServiceOk<Guid>(commandId);
        }
    }

    static (FundCashTransactionViewModel ViewModel, TestFundEventSource EventSource, IFundCommandApi CommandApi)
        CreateUnconfiguredSubject(FundTransactionType transactionType)
    {
        var eventConsumer = Substitute.For<IFundUIEventConsumer>();
        var eventSource = new TestFundEventSource(eventConsumer);
        var commandApi = Substitute.For<IFundCommandApi>();
        var fundCommandModel = new FundCommandService(
            commandApi,
            Substitute.For<IFundRiskMarginUIEventConsumer>(),
            Substitute.For<IFundOrderTradeStateUIEventConsumer>());
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.Services.FundCommands.Returns(fundCommandModel);
        appRoot.Services.FundEvents.Returns(new FundEventService(eventConsumer));
        appRoot.Services.StatusConsole.Returns(new StatusConsoleService(
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<IStatusConsoleEventConsumer>()));
        var fund = new FundReadModel(7, "G2 Fund", "fixture", 1000m, false, DateTime.UtcNow, "tests");
        return (
            new FundCashTransactionViewModel(
                appRoot, fund, new DateOnly(2026, 8, 18), transactionType),
            eventSource,
            commandApi);
    }

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

        public ValueTask PublishAsync(IEvent domainEvent)
            => _listener?.Invoke(domainEvent) ?? ValueTask.CompletedTask;
    }
}
