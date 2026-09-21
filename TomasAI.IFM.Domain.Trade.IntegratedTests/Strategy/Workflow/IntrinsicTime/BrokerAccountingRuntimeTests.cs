using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Theory, Trait("Category", "EventLogBrokerAccounting")]
    [InlineData(OrderExecutionStatus.Filled, false, 0, false)]
    [InlineData(OrderExecutionStatus.Cancelled, false, 0, false)]
    [InlineData(OrderExecutionStatus.Filled, true, 0, false)]
    [InlineData(OrderExecutionStatus.Cancelled, true, 0, false)]
    [InlineData(OrderExecutionStatus.Filled, true, 1000, false)]
    [InlineData(OrderExecutionStatus.Cancelled, true, -1000, false)]
    [InlineData(OrderExecutionStatus.Filled, false, 1000, false)]
    [InlineData(OrderExecutionStatus.Cancelled, false, -1000, false)]
    [InlineData(OrderExecutionStatus.Filled, true, 0.01, true)]
    [InlineData(OrderExecutionStatus.Filled, false, 1000, true)]
    public async Task Completed_fill_posts_through_real_accounting_API_and_ledger_actor_then_replays(
        OrderExecutionStatus status, bool partialClose, decimal initialMtm, bool breakEven)
    {
        if (EventLogEngineQualification.Validate() is null)
            throw new InvalidOperationException("This test requires the owned event-log qualification fixture.");
        await using var host = Host(brokerUrl: Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL"), actualPortfolio: true);
        _ = host.CreateClient();
        var supervisor = host.Services.GetRequiredService<IActorSupervisor>();
        var stopped = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            var transactions = FinancialBoundaryTransactions();
            await new PortfolioFinancialSchema(transactions).InitializeAsync();
            var portfolio = Random.Shared.Next(100000, 900000000);
            var fund = portfolio + 1;
            var book = new FinancialBookConfiguration
            {
                BookId=portfolio, PortfolioId=portfolio, AccountingEntityId=Guid.NewGuid(),
                ExecutionAccountReference=$"AccountingQualification/{Guid.NewGuid():N}", Environment="Emulator", MigrationQualified=true,
                Funds=[new() { FundId=fund, CanSpend=true, PortfolioStreamVersion=1, FundStreamVersion=1, PolicyStreamVersion=1,
                    Reference=new() { PolicyId=portfolio+2, ValidUntilUtc=DateTime.UtcNow.AddDays(1) } }]
            };
            await transactions.ExecuteAsync(async (db, token) =>
            {
                foreach (var stream in new[] { $"Portfolio.{portfolio}", $"PortfolioFund.{portfolio}.{fund}", $"PortfolioFinancialPolicy.{portfolio}.{portfolio+2}" })
                {
                    var id=Guid.NewGuid();
                    await db.AppendAsync(stream,id,new PortfolioCreated(Guid.NewGuid(),id,1,DateTime.UtcNow,"SyntheticAccountingQualification",new()),0,token);
                }
                return true;
            }, deadline.Token);
            LedgerPostingRule Rule(LedgerTransactionKind kind, int debit, int credit) =>
                new(Guid.NewGuid(),1,$"qualification-{kind}",kind,new(debit,1),new(credit,1),kind!=LedgerTransactionKind.Valuation,
                    kind==LedgerTransactionKind.RealizedPnl?new(106,1):null,kind==LedgerTransactionKind.RealizedPnl?new(107,1):null);
            var depositRule=Rule(LedgerTransactionKind.DepositConfirmed,101,102);
            var valuationRule=Rule(LedgerTransactionKind.Valuation,106,107);
            await new PortfolioFinancialStore(transactions).CreateBookAsync(book,
                [new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity"),new(103,1,"Expense",PostingSide.Debit,true,"expense"),
                    new(104,1,"Settlement clearing",PostingSide.Debit,true,"clearing"),new(105,1,"Realized PnL",PostingSide.Credit,true,"pnl"),
                    new(106,1,"Valuation asset",PostingSide.Debit,true,"Asset"),new(107,1,"Unrealized PnL",PostingSide.Credit,true,"UnrealizedPnl")],
                [depositRule,Rule(LedgerTransactionKind.TradeSettlement,104,101),Rule(LedgerTransactionKind.Commission,103,101),
                    Rule(LedgerTransactionKind.RealizedPnl,104,105),valuationRule],
                new(2020,1,1),new(2099,12,31));
            var actors=host.Services.GetRequiredService<IActorService>();
            var now=DateTime.UtcNow; var operation=Guid.NewGuid();
            var fundingAmount=partialClose?20000m:10000m;
            var openingCost=partialClose?10500m:5000m;
            var openingFee=partialClose?5m:2.50m;
            var openingCash=fundingAmount-openingCost-openingFee;
            var deposit=new PostFundTransactionCommand
            {
                CommandId=operation,OperationId=operation,PortfolioId=portfolio,EntityId=new(portfolio),
                Subject=new(ActorType.Command,PostFundTransactionCommand.Actor,PostFundTransactionCommand.Verb,portfolio.ToString()),
                CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(1),
                ExpectedFinancialRevision=0,Access=new("SyntheticAccountingQualification",["PortfolioAdministrator"]),
                Body=new() { BookId=portfolio,FundId=fund,Currency="USD",Amount=fundingAmount,TransactionKind=LedgerTransactionKind.DepositConfirmed,
                    AccountingDate=DateOnly.FromDateTime(now),ValueDate=DateOnly.FromDateTime(now),
                    PostingRule=new() { RuleId=depositRule.RuleId,Version=1,ContentHash=depositRule.ContentHash },
                    Source=new() { System="SyntheticAccountingQualification",SourceEventId=Guid.NewGuid(),SourceContentHash=new('A',64),OccurredAtUtc=now },
                    MovementEvidence=new() { Status=MovementStatus.Confirmed,SourceReference="Synthetic funding; no actual cash" } }
            };
            deposit=deposit with { InputSha256=FinancialCanonicalHash.Request(deposit) };
            var funding=await actors.SendAsync<PostFundTransactionCommand,LedgerPortfolioId>(deposit,deposit.EntityId,deadline.Token);
            funding.Success.Should().BeTrue(funding.ErrorMessage);
            var component=Guid.NewGuid(); var leg=Guid.NewGuid(); var attempt=Guid.NewGuid();
            var order=new TradeOrderDefinition
            {
                Id=new(portfolio,fund,1),PositionType=TradeOrderPositionType.Opening,ValueDate=DateOnly.FromDateTime(now),
                Components=[new() { ComponentId=component,ReservedTradeId=1,StrategyKind=TradeStrategyKind.FuturesOutright,
                    Legs=[new() { TradeLegId=leg,ContractId="ES-SYNTHETIC",SignedQuantity=partialClose?(status==OrderExecutionStatus.Filled?2:3):status==OrderExecutionStatus.Filled?1:2,CashMultiplier=50m }] }]
            };
            var execution=new OrderExecutionDefinition
            {
                TradeOrderId=order.Id,ExecutionAttemptId=attempt,Channel=ExecutionChannel.Broker,Status=status,
                PositionType=TradeOrderPositionType.Opening,Order=order,Components=order.Components,
                OrderQuantity=status==OrderExecutionStatus.Filled?1:2,CumulativeFilledQuantity=1,StartedAtUtc=now,CompletedAtUtc=now,
                Fills=[new() { ExecutionFillId=Guid.NewGuid(),ExecutionAttemptId=attempt,ComponentId=component,TradeLegId=leg,
                    ContractId="ES-SYNTHETIC",SignedQuantity=1,Price=100m,Commission=2.50m,FilledAtUtc=now,ExternalExecutionId=$"SYN-{attempt:N}" }]
            };
            if (partialClose)
                execution=execution with { OrderQuantity=status==OrderExecutionStatus.Filled?2:3,CumulativeFilledQuantity=2,
                    Fills=[execution.Fills[0],execution.Fills[0] with { ExecutionFillId=Guid.NewGuid(),Price=110m,
                        ExternalExecutionId=$"SYN-SECOND-{attempt:N}" }] };
            var api=host.Services.GetRequiredService<SimpleInjector.Container>().GetInstance<IPortfolioTradeAccountingApi>();
            var source=Guid.NewGuid();
            var first=await Task.WhenAll(
                api.PostConfirmedExecutionAsync(execution,source,now,deadline.Token).AsTask(),
                api.PostConfirmedExecutionAsync(execution,source,now,deadline.Token).AsTask());
            foreach(var reply in first) reply.Success.Should().BeTrue(reply.ErrorMessage);
            var queries=new FinancialQueryStore(transactions);
            var scope=new FinancialReadScope { PortfolioId=portfolio,FundId=fund,Access=new("SyntheticAccountingQualification",["PortfolioAdministrator"]) };
            var posted=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
            posted.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(openingCash);
            output.WriteLine($"First posting committed: portfolio={portfolio}, cash={openingCash}, revision={posted.FinancialRevision}");
            var replay=await api.PostConfirmedExecutionAsync(execution,source,now,deadline.Token);
            var after=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
            after.FinancialRevision.Should().Be(posted.FinancialRevision);
            after.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(openingCash);
            output.WriteLine($"Replay result: success={replay.Success}, error={replay.ErrorMessage}, cash unchanged, revision={after.FinancialRevision}");
            replay.Success.Should().BeTrue($"unchanged completed execution must replay after financial revision advances: {replay.ErrorMessage}");
            foreach (var changedFill in new[] {
                execution.Fills[0] with { Price=101m },
                execution.Fills[0] with { Commission=3m },
                execution.Fills[0] with { SignedQuantity=2 },
                execution.Fills[0] with { ExternalExecutionId="DIFFERENT" } })
            {
                var changed=execution with { Fills=[changedFill] };
                var conflict=await FluentActions.Awaiting(async () =>
                    await api.PostConfirmedExecutionAsync(changed,source,now,deadline.Token))
                    .Should().ThrowAsync<FinancialOperationException>();
                conflict.Which.Code.Should().Be(FinancialReasons.RequestMismatch);
            }
            var extraId=Guid.NewGuid();
            var extra=deposit with { CommandId=extraId,OperationId=extraId,ExpectedFinancialRevision=posted.FinancialRevision,
                Body=deposit.Body with { Amount=100m,Source=deposit.Body.Source with { SourceEventId=Guid.NewGuid() } } };
            extra=extra with { InputSha256=FinancialCanonicalHash.Request(extra) };
            (await actors.SendAsync<PostFundTransactionCommand,LedgerPortfolioId>(extra,extra.EntityId,deadline.Token))
                .Success.Should().BeTrue();
            var recreated=new BrokerExecutionAccountingApi(new FinancialQueryStore(FinancialBoundaryTransactions()),
                database.DbFactory,actors,FinancialBoundaryTransactions());
            var recovered=await recreated.PostConfirmedExecutionAsync(execution,Guid.NewGuid(),now.AddSeconds(1),deadline.Token);
            recovered.Success.Should().BeTrue(recovered.ErrorMessage);
            var final=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
            final.FinancialRevision.Should().Be(posted.FinancialRevision+1);
            final.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(openingCash+100m);
            await FluentActions.Awaiting(async () => await api.PostConfirmedExecutionAsync(
                execution with { Status=OrderExecutionStatus.Cancelled,Fills=[],CumulativeFilledQuantity=0 },
                source,now,deadline.Token)).Should().ThrowAsync<ArgumentException>();
            // Use actual persisted opening evidence for full and weighted-average partial closes.
            var tradeId=new TradeEntityId(portfolio,fund,1,1);
            var closeAttempt=Guid.NewGuid();
            var averagePrice=partialClose?105m:100m;
            var closePrice=breakEven?averagePrice:status==OrderExecutionStatus.Filled?120m:80m;
            var closeOrder=order with { Id=new(portfolio,fund,2),PositionType=TradeOrderPositionType.Closing,
                Components=[order.Components[0] with { Legs=[order.Components[0].Legs[0] with { SignedQuantity=-1 }] }] };
            var close=execution with { TradeOrderId=closeOrder.Id,ExecutionAttemptId=closeAttempt,
                Status=OrderExecutionStatus.Filled,PositionType=TradeOrderPositionType.Closing,
                TargetPositionId=StrategyPositionId.Create(tradeId,TradeStrategyKind.FuturesOutright),
                Order=closeOrder,Components=closeOrder.Components,OrderQuantity=1,CumulativeFilledQuantity=1,
                Fills=[execution.Fills[0] with { ExecutionFillId=Guid.NewGuid(),ExecutionAttemptId=closeAttempt,
                    SignedQuantity=-1,Price=closePrice,ExternalExecutionId=$"SYN-CLOSE-{closeAttempt:N}" }] };
            await FluentActions.Awaiting(async () => await api.PostConfirmedExecutionAsync(close,Guid.NewGuid(),now,deadline.Token))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("PORTFOLIO_ACCOUNTING.OPENING_TRADE_NOT_FOUND");
            var missingBasisBalance=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
            missingBasisBalance.FinancialRevision.Should().Be(final.FinancialRevision);
            await database.DbFactory.TradeDb.UpsertEstablishedTradeAsync(new EstablishedTradeDefinition
            {
                Id=tradeId,AssetFamily=TradeAssetFamily.Futures,StrategyKind=TradeStrategyKind.FuturesOutright,
                SourceComponentId=component,ExecutionAttemptId=attempt,Status=EstablishedTradeStatus.Open,
                Legs=order.Components[0].Legs,OriginalFills=execution.Fills,OpeningValue=openingCost,OpeningCommission=openingFee,
                EstablishedAtUtc=now,EvidenceRevision=1
            },deadline.Token);
            if(initialMtm!=0)
            {
                var valuationId=Guid.NewGuid();
                var mark=deposit with { CommandId=valuationId,OperationId=valuationId,ExpectedFinancialRevision=final.FinancialRevision,
                    Body=deposit.Body with { TransactionKind=LedgerTransactionKind.Valuation,Amount=initialMtm,
                        PostingRule=new() { RuleId=valuationRule.RuleId,Version=1,ContentHash=valuationRule.ContentHash },
                        Source=deposit.Body.Source with { SourceEventId=Guid.NewGuid(),SourceSequence=10,OrderId=tradeId.OrderId,TradeId=tradeId.TradeId } } };
                mark=mark with { InputSha256=FinancialCanonicalHash.Request(mark) };
                var marked=await actors.SendAsync<PostFundTransactionCommand,LedgerPortfolioId>(mark,mark.EntityId,deadline.Token);
                marked.Success.Should().BeTrue(marked.ErrorMessage);
                final=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
                final.Value!.Accounts.Single(x=>x.AccountId==106).Balance.Should().Be(initialMtm);
            }
            if(partialClose)
            {
                // Pause exactly after durable claim, before dispatch; competing executions cannot reuse basis.
                var pausedActors=new PausedAccountingActorService(actors);
                var pausedApi=new BrokerExecutionAccountingApi(queries,database.DbFactory,pausedActors,transactions);
                var pending=pausedApi.PostConfirmedExecutionAsync(close,Guid.NewGuid(),now,deadline.Token).AsTask();
                try
                {
                    await pausedActors.Reached.Task.WaitAsync(deadline.Token);
                    var competingAttempt=Guid.NewGuid();
                    var competing=close with { ExecutionAttemptId=competingAttempt,
                        Fills=[close.Fills[0] with { ExecutionAttemptId=competingAttempt,ExecutionFillId=Guid.NewGuid(),
                            ExternalExecutionId=$"COMPETING-{competingAttempt:N}" }] };
                    await FluentActions.Awaiting(async()=>await api.PostConfirmedExecutionAsync(competing,Guid.NewGuid(),now,deadline.Token))
                        .Should().ThrowAsync<InvalidOperationException>()
                        .WithMessage("PORTFOLIO_ACCOUNTING.PRIOR_CLOSE_PENDING_RECONCILIATION");
                    var held=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
                    held.FinancialRevision.Should().Be(final.FinancialRevision);
                }
                finally { pausedActors.Release.TrySetResult(); }
                (await pending).Success.Should().BeTrue();
            }
            var closeReplies=await Task.WhenAll(
                api.PostConfirmedExecutionAsync(close,Guid.NewGuid(),now,deadline.Token).AsTask(),
                api.PostConfirmedExecutionAsync(close,Guid.NewGuid(),now,deadline.Token).AsTask());
            foreach(var reply in closeReplies) reply.Success.Should().BeTrue(reply.ErrorMessage);
            var closed=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
            var expectedCash=openingCash+100m+closePrice*50m-2.50m;
            closed.FinancialRevision.Should().Be(final.FinancialRevision+1);
            closed.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(expectedCash);
            closed.Value.Accounts.Single(x=>x.AccountId==103).Balance.Should().Be(openingFee+2.50m);
            closed.Value.Accounts.Single(x=>x.AccountId==104).Balance.Should().Be(partialClose?5250m:0m);
            // Stored balances are debit minus credit, including credit-normal P&L accounts.
            var pnl=closed.Value.Accounts.Where(x=>x.AccountId==105).ToArray();
            pnl.Sum(x=>x.Balance).Should().Be(-(closePrice-averagePrice)*50m);
            pnl.Sum(x=>x.Credits-x.Debits).Should().Be((closePrice-averagePrice)*50m);
            closed.Value.Accounts.Sum(x=>x.Debits).Should().Be(closed.Value.Accounts.Sum(x=>x.Credits));
            if(initialMtm!=0)
            {
                var expectedRemaining=partialClose?initialMtm-decimal.Round(initialMtm/2,2,MidpointRounding.ToEven):0;
                closed.Value.Accounts.Single(x=>x.AccountId==106).Balance.Should().Be(expectedRemaining);
                closed.Value.Accounts.Single(x=>x.AccountId==107).Balance.Should().Be(-expectedRemaining);
                var storedMark=await transactions.ExecuteAsync((db,ct)=>db.ScalarAsync(
                    "SELECT amount FROM portfolio_financial.ledger_valuation WHERE book_id=$1 AND fund_id=$2 AND position_key=$3;",
                    [portfolio,fund,$"{tradeId.OrderId}:{tradeId.TradeId}"],ct),deadline.Token);
                storedMark.Should().Be(expectedRemaining);
            }
            output.WriteLine($"Close committed: partial={partialClose}, cash={expectedCash}, gross PnL={(closePrice-averagePrice)*50m}, revision={closed.FinancialRevision}");
            OrderExecutionDefinition? secondClose=null;
            if(partialClose)
            {
                var nextAttempt=Guid.NewGuid();
                secondClose=close with { ExecutionAttemptId=nextAttempt,
                    Fills=[close.Fills[0] with { ExecutionAttemptId=nextAttempt,ExecutionFillId=Guid.NewGuid(),
                        ExternalExecutionId=$"SYN-FINAL-{nextAttempt:N}" }] };
                var next=await api.PostConfirmedExecutionAsync(secondClose,Guid.NewGuid(),now,deadline.Token);
                next.Success.Should().BeTrue(next.ErrorMessage);
                closed=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
                expectedCash+=closePrice*50m-2.50m;
                closed.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(expectedCash);
                closed.Value.Accounts.Single(x=>x.AccountId==104).Balance.Should().Be(0m);
                closed.Value.Accounts.Where(x=>x.AccountId==105).Sum(x=>x.Balance).Should().Be(-2*(closePrice-105m)*50m);
                closed.FinancialRevision.Should().Be(final.FinancialRevision+2);
                if(initialMtm!=0)
                {
                    closed.Value.Accounts.Single(x=>x.AccountId==106).Balance.Should().Be(0);
                    closed.Value.Accounts.Single(x=>x.AccountId==107).Balance.Should().Be(0);
                }
                var overAttempt=Guid.NewGuid();
                var over=secondClose with { ExecutionAttemptId=overAttempt,
                    Fills=[secondClose.Fills[0] with { ExecutionAttemptId=overAttempt,ExecutionFillId=Guid.NewGuid(),ExternalExecutionId=$"OVER-{overAttempt:N}" }] };
                await FluentActions.Awaiting(async()=>await api.PostConfirmedExecutionAsync(over,Guid.NewGuid(),now,deadline.Token))
                    .Should().ThrowAsync<ArgumentException>();
            }
            var consumed=await transactions.ExecuteAsync(async(db,ct)=>
                await db.QueryAsync("""
                    SELECT count(*),sum(closed_quantity),sum(allocated_signed_basis)
                    FROM portfolio_financial.broker_closing_basis_claim WHERE portfolio_id=$1 AND position_key=$2;
                    """,[portfolio,close.TargetPositionId!.Value.Format()],r=>(Count:r.GetInt64(0),Quantity:r.GetDecimal(1),Basis:r.GetDecimal(2)),ct),deadline.Token);
            consumed.Single().Count.Should().Be(partialClose?2:1);
            consumed.Single().Quantity.Should().Be(partialClose?2:1);
            consumed.Single().Basis.Should().Be(openingCost);
            await supervisor.ShutdownAsync();
            await host.DisposeAsync();
            stopped=true;
            await using var restartedHost=Host(brokerUrl:Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL"),actualPortfolio:true);
            _=restartedHost.CreateClient();
            var restartedSupervisor=restartedHost.Services.GetRequiredService<IActorSupervisor>();
            try
            {
                var restartedApi=restartedHost.Services.GetRequiredService<SimpleInjector.Container>()
                    .GetInstance<IPortfolioTradeAccountingApi>();
                var restarted=await restartedApi.PostConfirmedExecutionAsync(execution,Guid.NewGuid(),now.AddSeconds(2),deadline.Token);
                restarted.Success.Should().BeTrue(restarted.ErrorMessage);
                var restartedBalance=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
                restartedBalance.FinancialRevision.Should().Be(closed.FinancialRevision);
                restartedBalance.Value!.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(expectedCash);
                var closeReplay=await restartedApi.PostConfirmedExecutionAsync(close,Guid.NewGuid(),now.AddSeconds(3),deadline.Token);
                closeReplay.Success.Should().BeTrue(closeReplay.ErrorMessage);
                if(secondClose is not null)
                    (await restartedApi.PostConfirmedExecutionAsync(secondClose,Guid.NewGuid(),now.AddSeconds(4),deadline.Token))
                        .Success.Should().BeTrue();
                var replayedClose=await queries.ReadAsync(scope,new GetAccountBalancesRequest(),deadline.Token);
                replayedClose.FinancialRevision.Should().Be(closed.FinancialRevision);
                replayedClose.Value!.Accounts.Should().BeEquivalentTo(closed.Value!.Accounts);
            }
            finally { await restartedSupervisor.ShutdownAsync(); }
        }
        finally { if(!stopped) await supervisor.ShutdownAsync(); }
    }

    private sealed class PausedAccountingActorService(IActorService inner) : IActorService
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<ServiceResult<Guid>> SendAsync<TCommand,TEntityId>(TCommand command,TEntityId entityId)
            where TCommand:class,ICommand<TEntityId> where TEntityId:IActorEntityId =>
            SendAsync(command,entityId,CancellationToken.None);
        public async ValueTask<ServiceResult<Guid>> SendAsync<TCommand,TEntityId>(TCommand command,TEntityId entityId,CancellationToken token)
            where TCommand:class,ICommand<TEntityId> where TEntityId:IActorEntityId
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(token);
            return await inner.SendAsync(command,entityId,token);
        }
        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult,TQuery>(TQuery query)
            where TQuery:class,IQuery<TResult> where TResult:class => inner.RequestAsync<TResult,TQuery>(query);
        public ValueTask<ServiceResult<Guid>> RequestAsync<TCommand,TEntityId>(TCommand command)
            where TCommand:class,ICommand<TEntityId> where TEntityId:IActorEntityId => inner.RequestAsync<TCommand,TEntityId>(command);
    }
}
