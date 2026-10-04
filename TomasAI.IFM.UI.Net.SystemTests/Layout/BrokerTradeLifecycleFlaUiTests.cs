using System.Reflection;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using System.Runtime.InteropServices;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using System.Windows.Forms;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using DataGridView = System.Windows.Forms.DataGridView;
using Label = System.Windows.Forms.Label;
using TreeView = System.Windows.Forms.TreeView;
using TabControl = System.Windows.Forms.TabControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimpleInjector;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Views.Trade;
using BrokerOrderType = TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType;
using BrokerAlgorithm = TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm;
using BrokerEnvironment = TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

[CollectionDefinition("Broker emulator UI", DisableParallelization = true)]
public sealed class BrokerUiCollection : ICollectionFixture<BrokerUiHost> { }

public sealed class BrokerUiHost : IAsyncLifetime
{
    public KestrelWebApplicationFactory<ApiServerEntryPoint> Host = null!;
    public IActorProducer Producer = null!;
    public ITradeBroker Broker = null!;
    public Guid Approval = Guid.NewGuid();
    public CatalogKey Deployment = new(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1);
    static CapacityLimit[] Limits(CapacityScopeKind scope, string key) =>
    [
        new(scope,key,CapacityMeasure.SettlementCash,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.LossCharge,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.Margin,CapacityUnit.Usd,1_000_000),
        new(scope,key,CapacityMeasure.GrossNotional,CapacityUnit.Usd,10_000_000),
        new(scope,key,CapacityMeasure.PositionSlots,CapacityUnit.Positions,100),
        new(scope,key,CapacityMeasure.GrossContracts,CapacityUnit.Contracts,100)
    ];
    public async Task InitializeAsync()
    {
        await DomainActorIntegrationInfrastructureFixture.InitializeAsync("UI.Net.SystemTests");
        Host = new KestrelWebApplicationFactory<ApiServerEntryPoint>().WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.BrokerAccount,TomasAI.IFM.Domain.Portfolio")
            .UseSetting("IFM_TEST_NATS_URL", DomainActorIntegrationInfrastructureFixture.NatsUrl)
            .UseSetting("TradeBroker:Emulator:LedgerPath", Path.Combine(AppContext.BaseDirectory, "data", "trade-broker", $"flaui-{Guid.NewGuid():N}.json")));
        using var client = Host.CreateClient();
        await Host.Services.GetRequiredService<ApplicationSchemaInitializer>().InitializeAsync();
        Broker = Host.Services.GetRequiredService<Container>().GetInstance<ITradeBroker>();
        await SeedAccountingAsync();
        Producer = Host.Services.GetRequiredService<IActorProducer>();
        await Producer.StartAsync(new(ActorType.Realtime, "FlaUiBrokerVerification"));
        var query = new BrokerAccountQueryApi(Producer);
        var accountId = new BrokerAccountId(Broker.AccountAlias);
        await Wait(async () => (await query.GetAsync(accountId)).Value?.Snapshot is { Complete: true });
        var commands = new BrokerAccountCommandApi(Producer);
        var manifest = Guid.NewGuid().ToString("N");
        Assert.True((await commands.SubmitQualificationEvidenceAsync(accountId, manifest, "isolated-FlaUI-test", DateTime.UtcNow)).Success);
        Assert.True((await commands.AcceptQualificationAsync(accountId, Approval, manifest, "isolated-test-reviewer", DateTime.UtcNow)).Success);
        await Wait(async () => (await query.GetAsync(accountId)).Value?.Gate == BrokerAccountOperationalGate.Open);
    }
    async Task SeedAccountingAsync()
    {
        var transactions = Host.Services.GetRequiredService<IPostgresEventTransaction>();
        await new PortfolioFinancialSchema(transactions).InitializeAsync();
        var book = new FinancialBookConfiguration { BookId = 1, PortfolioId = 1,
            AccountingEntityId = Guid.NewGuid(), ExecutionAccountReference = "SyntheticFlaUI", Environment = "Emulator",
            MigrationQualified = true, Funds = [new() { FundId = 4, CanSpend = true,
                PortfolioStreamVersion = 1, FundStreamVersion = 1, PolicyStreamVersion = 1,
                Limits = Limits(CapacityScopeKind.Portfolio, FinancialScopeKeys.Portfolio(1))
                    .Concat(Limits(CapacityScopeKind.Fund, FinancialScopeKeys.Fund(4))).ToArray(),
                Deployments = [new FinancialDeploymentAuthority(
                    new() { PolicyId = 3, DeploymentKey = Deployment, ValidUntilUtc = DateTime.UtcNow.AddDays(1) },
                    Limits(CapacityScopeKind.Deployment, FinancialScopeKeys.Deployment(Deployment)),100_000)],
                Reference = new() { PolicyId = 3, ValidUntilUtc = DateTime.UtcNow.AddDays(1) } }] };
        await transactions.ExecuteAsync(async (db, token) => {
            foreach(var stream in new[] { "Portfolio.1", "PortfolioFund.1.4", "PortfolioFinancialPolicy.1.3" }) {
                var id = Guid.NewGuid();
                await db.AppendAsync(stream,id,new PortfolioCreatedEvent(Guid.NewGuid(),id,1,DateTime.UtcNow,"SyntheticFlaUI",new()),0,token);
            }
            return true;
        });
        LedgerPostingRule Rule(LedgerTransactionKind kind,int debit,int credit) => new(Guid.NewGuid(),1,
            $"FlaUI-{kind}",kind,new(debit,1),new(credit,1),kind!=LedgerTransactionKind.Valuation,
            kind==LedgerTransactionKind.RealizedPnl ? new(106,1) : null,
            kind==LedgerTransactionKind.RealizedPnl ? new(107,1) : null);
        var depositRule = Rule(LedgerTransactionKind.DepositConfirmed,101,102);
        var accounts = new LedgerAccountDefinition[] { new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity"),
             new(103,1,"Expense",PostingSide.Debit,true,"expense"),new(104,1,"Clearing",PostingSide.Debit,true,"clearing"),
             new(105,1,"PnL",PostingSide.Credit,true,"pnl"),new(106,1,"Valuation",PostingSide.Debit,true,"Asset"),
             new(107,1,"Unrealized",PostingSide.Credit,true,"UnrealizedPnl") };
        var rules = new LedgerPostingRule[] { depositRule,Rule(LedgerTransactionKind.TradeSettlement,104,101),Rule(LedgerTransactionKind.Commission,103,101),
             Rule(LedgerTransactionKind.RealizedPnl,104,105),Rule(LedgerTransactionKind.Valuation,106,107) };
        await (Task)typeof(PortfolioFinancialStore).GetMethod("CreateBookAsync",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(new PortfolioFinancialStore(transactions),[book,accounts,rules,new DateOnly(2020,1,1),new DateOnly(2099,12,31),CancellationToken.None])!;
        var now = DateTime.UtcNow; var operation = Guid.NewGuid();
        var deposit = new PostFundTransactionCommand { CommandId = operation,OperationId = operation,PortfolioId = 1,EntityId = new(1),
            Subject = new(ActorType.Command,PostFundTransactionCommand.Actor,PostFundTransactionCommand.Verb,"1"),
            CorrelationId = Guid.NewGuid(),CausationId = Guid.NewGuid(),RequestedAtUtc = now,ExpiresAtUtc = now.AddMinutes(1),
            Access = new("SyntheticFlaUI",["PortfolioAdministrator"]),Body = new() { BookId = 1,FundId = 4,Currency = "USD",Amount = 1000000m,
                TransactionKind = LedgerTransactionKind.DepositConfirmed,AccountingDate = DateOnly.FromDateTime(now),ValueDate = DateOnly.FromDateTime(now),
                PostingRule = new() { RuleId = depositRule.RuleId,Version = 1,ContentHash = depositRule.ContentHash },
                Source = new() { System = "SyntheticFlaUI",SourceEventId = Guid.NewGuid(),SourceContentHash = new('A',64),OccurredAtUtc = now },
                MovementEvidence = new() { Status = MovementStatus.Confirmed,SourceReference = "Synthetic funding only" } } };
        deposit = deposit with { InputSha256 = FinancialCanonicalHash.Request(deposit) };
        var result = await Host.Services.GetRequiredService<IActorService>().SendAsync<PostFundTransactionCommand,LedgerPortfolioId>(deposit,deposit.EntityId);
        Assert.True(result.Success,result.ErrorMessage);
    }
    public async Task DisposeAsync()
    {
        if (Producer is not null) await Producer.StopAsync();
        if (Host is not null)
        {
            await Host.Services.GetRequiredService<ISupervisorManagedActorLifecycle>().ShutdownActorsAsync(CancellationToken.None);
            await Host.DisposeAsync();
        }
        await DomainActorIntegrationInfrastructureFixture.DisposeAsync();
    }
    public static async Task Wait(Func<Task<bool>> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (!await predicate()) await Task.Delay(100, deadline.Token);
    }
}

// Reference data is a deterministic fixture port. Portfolio approval and all Trade Order,
// Order Execution and Broker Order commands, projections, storage and notifications use
// the real isolated API server / NATS / emulator. No real account is contacted.
[Collection("Broker emulator UI")]
public sealed class BrokerTradeLifecycleFlaUiTests(BrokerUiHost host)
{
    static int contractSequence;
    [Fact] public Task Offline_condor_simulation_completes_through_ui_actors_and_accounting() => RunLifecycleAsync(TradeType.ShortIronCondor, true, TradeAction.Buy, BrokerOrderType.Limit, true);
    [Fact] public Task Futures_publications_after_fill() => Fresh_screen_places_updates_and_cancels_or_fills_via_emulator(TradeType.FuturesOutright, true, TradeAction.Buy, BrokerOrderType.Limit);
    [Fact] public Task Condor_publications_after_fill() => Fresh_screen_places_updates_and_cancels_or_fills_via_emulator(TradeType.ShortIronCondor, true, TradeAction.Buy, BrokerOrderType.Limit);
    [Fact] public Task Spread_publications_after_fill() => Fresh_screen_places_updates_and_cancels_or_fills_via_emulator(TradeType.CallDebitSpread, true, TradeAction.Buy, BrokerOrderType.Limit);
    [Theory]
    [InlineData(TradeType.ShortIronCondor, false, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.PutCreditSpread, false, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.FuturesOutright, false, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.ShortIronCondor, true, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.CallDebitSpread, true, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.FuturesOutright, true, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.LongIronCondor, false, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.PutDebitSpread, false, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.CallCreditSpread, false, TradeAction.Buy, BrokerOrderType.Limit)]
    [InlineData(TradeType.FuturesOutright, false, TradeAction.Sell, BrokerOrderType.Limit)]
    [InlineData(TradeType.FuturesOutright, true, TradeAction.Sell, BrokerOrderType.Limit)]
    [InlineData(TradeType.FuturesOutright, true, TradeAction.Buy, BrokerOrderType.Market)]
    [InlineData(TradeType.ShortIronCondor, true, TradeAction.Buy, BrokerOrderType.Market)]
    public Task Fresh_screen_places_updates_and_cancels_or_fills_via_emulator(TradeType type, bool fill, TradeAction action, BrokerOrderType orderType) => RunLifecycleAsync(type,fill,action,orderType,false);
    private async Task RunLifecycleAsync(TradeType type, bool fill, TradeAction action, BrokerOrderType orderType, bool simulate)
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>(); root.Services.Returns(services);
        var sequence = Interlocked.Increment(ref contractSequence);
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10 + sequence));
        var futureExpiry = new DateOnly(2026,12,18).AddMonths(sequence);
        // Each case has its own instruments; a prior case's emulator quote must not fill a new order before update/cancel.
        var underlying = new FuturesContractV3ReadModel($"ES{futureExpiry:yyyyMMdd}", "ES", "ES", "ESZ6", "FUT", "USD", "CME", "50", futureExpiry, true);
        var market = Substitute.For<IMarketDataQueryApi>();
        var feed = Substitute.For<IMarketDataFeedQueryApi>();
        feed.GetFuturesEodDataAsync(Arg.Any<string>(),Arg.Any<DateOnly>()).Returns(new ServiceFailed<FuturesEodDataV2ReadModel>(404,"Synthetic test has no EOD"));
        feed.GetLastFuturesEodDataAsync(Arg.Any<string>(),Arg.Any<DateOnly>()).Returns(new ServiceFailed<FuturesEodDataV2ReadModel>(404,"Synthetic test has no EOD"));
        market.GetValueDateAsync().Returns(new ServiceOk<ScalarReadModel<DateOnly>>(new(DateOnly.FromDateTime(DateTime.UtcNow))));
        market.GetDatabentoOptionChainRangeAsync(Arg.Any<string>(),Arg.Any<DateOnly>(),Arg.Any<DateOnly>()).Returns(new ServiceFailed<OptionContractExpiryReadModel[]>(404,"Fixture supplies the chain"));
        var parameters = Substitute.For<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi>();
        parameters.StartupRunsAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterStartupRun[]>([]));
        services.ParameterSets.Returns(parameters);
        market.GetFuturesContractAsync(Arg.Any<string>()).Returns(new ServiceOk<FuturesContractV3ReadModel>(underlying));
        services.MarketDataQueries.Returns(new MarketDataQueryService(market, feed));
        var portfolio = Substitute.For<IPortfolioQueryApi>(); services.PortfolioQueries.Returns(portfolio);
        portfolio.GetFundAsync(1, 4, Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundMandateReadModel>(new() { FundMandateVersion = 1 }));
        var strategy = type == TradeType.FuturesOutright ? TradeStrategyKind.FuturesOutright : type is TradeType.ShortIronCondor or TradeType.LongIronCondor ? TradeStrategyKind.IronCondor : TradeStrategyKind.VerticalSpread;
        portfolio.GetAssignmentsAsync(1,4,1,Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([new() {
            Enabled = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1), UnderlyingUniverse = ["ES"],
            TradeFamily = strategy == TradeStrategyKind.FuturesOutright ? "Futures" : strategy.ToString(),
            TradeStrategyFamily = new(0,0) { CatalogDeployment = host.Deployment }
        }]));
        services.BrokerAccounts.Returns(new BrokerAccountQueryApi(host.Producer));
        services.BrokerOrders.Returns(new BrokerOrderQueryApi(host.Producer));
        services.OrderExecutions.Returns(new OrderExecutionQueryApi(host.Producer));
        services.BrokerOrderCommands.Returns(new BrokerOrderCommandApi(host.Producer));
        services.OrderExecutionNotifications.Returns(new OrderExecutionNotificationService(() => new NatsActorEventListener(
            host.Host.Services.GetRequiredService<INatsEventListenerOptions>(), NullLogger.Instance,
            host.Host.Services.GetRequiredService<NatsConnectionManager>())));
        PortfolioOrderCandidate? captured = null;
        var approvals = Substitute.For<IPortfolioOrderCompositionApi>();
        approvals.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<EvaluatePortfolioOrderCompositionCommand>(); captured = request.Body;
            return new PortfolioFinancialApi(host.Producer).EvaluateAsync(request, call.Arg<CancellationToken>());
        });
        services.PortfolioTradeOrders.Returns(new PortfolioTradeOrderService(approvals,new TradeOrderLifecycleApi(host.Producer)));
        var trade = new PortfolioFundOrderTradeEditorModel { PortfolioId = 1, FundId = 4, OrderId = 10, TradeId = 1,
            TradeState = TradeState.NewTrade, TradeType = type, TradeAction = action,
            BaseContractId = underlying.ContractId, BaseContractSymbol = "ES", UnderlyingRoot = "ES",
            RequestedTradeDate = DateOnly.FromDateTime(DateTime.UtcNow), RequestedMaturityDate = expiry };
        var fund = new PortfolioFundEditorModel(4,"Synthetic FlaUI Fund","",0,false,DateTime.UtcNow,"test");
        var fundOrder = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { PortfolioId = 1,FundId = 4,OrderId = 10 });
        var ready = new TaskCompletionSource<Form>(TaskCreationOptions.RunContinuationsAsynchronously);
        EsTradeBlotterControl? blotter = null; Task<Guid>? submitting = null;
        var ui = new Thread(() => {
            using var form = new Form { Text = "FlaUI broker lifecycle", Width = 1400, Height = 850 };
            using var legacy = new UnloadedLegacy();
            blotter = new(root,fund,fundOrder,trade,1,false,BrokerCapabilities.Emulator(host.Broker.AccountAlias),legacy);
            form.Controls.Add(blotter);
            blotter.SubmitOpeningRequested += (_,_) => submitting = blotter.SubmitOrderAsync(trade.RequestedTradeDate,OrderActionType.Open,new WinFormsTradeOrderConfirmationService(form));
            form.Shown += (_,_) => ready.TrySetResult(form);
            System.Windows.Forms.Application.Run(form);
        }) { IsBackground = true }; ui.SetApartmentState(ApartmentState.STA); ui.Start();
        var windowForm = await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
        async Task OnUi(Action action) {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            windowForm.BeginInvoke((Action)(() => { try { action(); done.SetResult(); } catch(Exception e) { done.SetException(e); } }));
            await done.Task;
        }
        var notifiedTrades = new ConcurrentDictionary<int, byte>();
        var notifiedPositions = new ConcurrentDictionary<int, byte>();
        var publicationListener = new NatsActorEventListener(
            host.Host.Services.GetRequiredService<INatsEventListenerOptions>(), NullLogger.Instance,
            host.Host.Services.GetRequiredService<NatsConnectionManager>());
        await publicationListener.StartAsync($"FlaUiFilledPublications-{Guid.NewGuid():N}", new()
        {
            [new(ActorType.Event, "FuturesTradeEvent")] = [FuturesTradeChangedEvent.Verb],
            [new(ActorType.Event, "FuturesOptionTradeEvent")] = [OptionTradeChangedEvent.Verb],
            [new(ActorType.Event, "FuturesTradePositionEvent")] = [PositionChangedEvent.Verb],
            [new(ActorType.Event, "FuturesIronCondorTradePositionEvent")] = [PositionChangedEvent.Verb],
            [new(ActorType.Event, "FuturesVerticalSpreadTradePositionEvent")] = [PositionChangedEvent.Verb]
        }, (verb, message) =>
        {
            if (verb == FuturesTradeChangedEvent.Verb)
                notifiedTrades.TryAdd(message.AsEvent<FuturesTradeChangedEvent>()!.EntityId.OrderId, 0);
            else if (verb == OptionTradeChangedEvent.Verb)
                notifiedTrades.TryAdd(message.AsEvent<OptionTradeChangedEvent>()!.EntityId.OrderId, 0);
            else if (message.Subject.ToSubject().Name == "FuturesTradePositionEvent")
                notifiedPositions.TryAdd(message.AsEvent<FuturesPositionChangedEvent>()!.EntityId.Trade.OrderId, 0);
            else if (message.Subject.ToSubject().Name == "FuturesIronCondorTradePositionEvent")
                notifiedPositions.TryAdd(message.AsEvent<IronCondorPositionChangedEvent>()!.EntityId.Trade.OrderId, 0);
            else if (message.Subject.ToSubject().Name == "FuturesVerticalSpreadTradePositionEvent")
                notifiedPositions.TryAdd(message.AsEvent<VerticalSpreadPositionChangedEvent>()!.EntityId.Trade.OrderId, 0);
            return ValueTask.CompletedTask;
        });
        try {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(windowForm.Handle).AsWindow();
            FlaUI.Core.AutomationElements.AutomationElement Find(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id)) ?? throw new InvalidOperationException(id);
            // Seed the reference/quote fixture before interacting with the screen. No legacy legs exist.
            BrokerTradeLegData[] fixtureLegs = [];
            await OnUi(() => {
                blotter!.BindAvailableExpiries([expiry],trade.RequestedTradeDate,expiry);
                var preview = (BrokerTradePreviewControl)blotter.Controls.Find("brokerTradePreview",true).Single();
                var init = new BrokerTradeInitializationResult(1,4,10,1,null,null,null,null,null,null,underlying,null,[],BrokerCapabilities.Emulator(host.Broker.AccountAlias),null,[]);
                typeof(EsTradeBlotterControl).GetField("_brokerInitialization",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(blotter,init);
                BrokerTradeLegData[] legs = strategy == TradeStrategyKind.FuturesOutright
                    ? [new(underlying.ContractId,"+Future",null,10,10.05m,1,true,Multiplier:50,IsFuture:true)]
                    : strategy == TradeStrategyKind.IronCondor
                    ? [new($"ES{expiry:yyyyMMdd}P95","+LP",95,1,1.05m,-.1,false,Multiplier:50),new($"ES{expiry:yyyyMMdd}P100","-SP",100,2,2.05m,-.2,false,Multiplier:50),new($"ES{expiry:yyyyMMdd}C110","-SC",110,2,2.05m,.2,true,Multiplier:50),new($"ES{expiry:yyyyMMdd}C115","+LC",115,1,1.05m,.1,true,Multiplier:50)]
                    : [new($"ES{expiry:yyyyMMdd}{(type is TradeType.PutCreditSpread or TradeType.PutDebitSpread ? "P" : "C")}95",type is TradeType.PutCreditSpread or TradeType.PutDebitSpread ? "+LP" : "+LC",95,1,1.05m,.2,type is not (TradeType.PutCreditSpread or TradeType.PutDebitSpread),Multiplier:50),new($"ES{expiry:yyyyMMdd}{(type is TradeType.PutCreditSpread or TradeType.PutDebitSpread ? "P" : "C")}100",type is TradeType.PutCreditSpread or TradeType.PutDebitSpread ? "-SP" : "-SC",100,2,2.05m,.1,type is not (TradeType.PutCreditSpread or TradeType.PutDebitSpread),Multiplier:50)];
                fixtureLegs = legs;
                var chain = new EvaluatedOptionChainReadModel(underlying.ContractId,expiry,105m,null,null,"Frozen emulator preview fixture",DateTimeOffset.UtcNow,
                    legs.Where(x=>!x.IsFuture).Select(x=>new EvaluatedOptionContractReadModel(x.ContractId,x.Strike!.Value,x.IsCall,x.Bid,x.Ask,10,10,null,null,.2,null,x.Delta,null,1,null,null,null,null,true,false,DateTimeOffset.UtcNow,null,DateTimeOffset.UtcNow)).ToArray());
                market.GetEvaluatedOptionChainAsync(Arg.Any<TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery>(),Arg.Any<CancellationToken>()).Returns(new ServiceOk<EvaluatedOptionChainReadModel>(chain));
                typeof(EsTradeBlotterControl).GetMethod("BindEvaluatedChain",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(blotter,[chain]);
            });
            // Use the actual Market Selection click path to assign all option legs.
            foreach(var leg in fixtureLegs.Where(x=>!x.IsFuture)) {
                var rows = fixtureLegs.Where(x=>!x.IsFuture).Select(x=>x.Strike!.Value).Distinct().OrderByDescending(x=>x).ToArray();
                var row = Array.IndexOf(rows,leg.Strike!.Value);
                var grid = Find("marketSelectionGrid").AsDataGridView();
                var cell = grid.Rows[row].Cells[leg.IsCall ? 4 : 7];
                Assert.True(cell.Patterns.Invoke.IsSupported,"Market Selection cell must expose the UI Automation invoke action.");
                cell.Patterns.Invoke.Pattern.Invoke();
                await Task.Delay(100);
            }
            window.FindFirstDescendant(cf=>cf.ByName("Broker Trade").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem))).AsTabItem().Select();
            await Task.Delay(300);
            Find("brokerPreviewContractsValue").FindFirstDescendant(cf=>cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)).AsTextBox().Text = "2";
            SelectCombo(Find("brokerPreviewOrdertypeSelector"), orderType.ToString(), automation);
            SelectCombo(Find("brokerPreviewTimeinforceSelector"), "GTC", automation);
            SelectCombo(Find("brokerPreviewIFMalgorithmSelector"), "Adaptive", automation);
            SelectCombo(Find("brokerPreviewPaceSelector"), "Urgent", automation);
            // Settings refresh the selection; restore only the synthetic quote input, not order state.
            await OnUi(() => { var preview = (BrokerTradePreviewControl)blotter!.Controls.Find("brokerTradePreview",true).Single();
                if (preview.SelectedLegs.Count == 0) throw new InvalidOperationException("Selection was erased by execution choices.");
                Assert.Equal("GTC", preview.SelectedTimeInForce);
                Assert.Equal("Adaptive", preview.SelectedAlgorithm);
                Assert.Equal("Urgent", preview.SelectedPace); });
            var limit = strategy == TradeStrategyKind.FuturesOutright ? action == TradeAction.Sell ? -9m : 11m : 1m;
            Find("brokerPreviewNetLimitTicks").AsTextBox().Text = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var placeInvocation = Task.Run(()=>Find("brokerPreviewPlaceOrder").AsButton().Invoke());
            IntPtr confirmationHandle = IntPtr.Zero;
            await BrokerUiHost.Wait(async () => {
                if (placeInvocation.IsFaulted) await placeInvocation;
                if (submitting?.IsFaulted == true) await submitting;
                await OnUi(() => {
                    var dialog = System.Windows.Forms.Application.OpenForms.OfType<TradeOrderConfirmationForm>().SingleOrDefault();
                    if (dialog is { Visible: true }) confirmationHandle = dialog.Handle;
                });
                return confirmationHandle != IntPtr.Zero;
            });
            var confirmation = automation.FromHandle(confirmationHandle).AsWindow();
            Assert.Equal("Trade Order Confirmation",confirmation.Title);
            SelectCombo(confirmation.FindFirstDescendant(cf=>cf.ByAutomationId("ddlTradeFillType")), "Broker", automation);
            confirmation.FindFirstDescendant(cf=>cf.ByAutomationId("btnContinue")).AsButton().Invoke();
            await placeInvocation.WaitAsync(TimeSpan.FromSeconds(10));
            await BrokerUiHost.Wait(() => Task.FromResult(submitting is not null));
            Assert.NotEqual(Guid.Empty,await submitting!.WaitAsync(TimeSpan.FromSeconds(40)));
            Assert.NotNull(captured); Assert.Equal(strategy,(TradeStrategyKind)captured!.StrategyKind);
            Assert.Equal((PortfolioBrokerOrderType)orderType, captured.BrokerOrderType);
            Assert.Equal(PortfolioBrokerAlgorithm.Adaptive, captured.BrokerAlgorithm);
            Assert.Equal("GTC",captured.TimeInForce); Assert.Equal("Urgent",captured.AlgorithmPace);
            Assert.All(captured.Components[0].Legs,leg=>Assert.Equal(2,Math.Abs(leg.SignedQuantity)));
            Assert.Equal(fixtureLegs.Select(x=>x.ContractId).OrderBy(x=>x),captured.Components[0].Legs.Select(x=>x.ContractId).OrderBy(x=>x));
            if(strategy == TradeStrategyKind.FuturesOutright)
                Assert.Equal(action == TradeAction.Sell ? -2 : 2,captured.Components[0].Legs[0].SignedQuantity);
            var submitted = blotter!.SubmittedTradeOrders.Single();
            var query = new BrokerOrderQueryApi(host.Producer); BrokerOrderDefinition? brokerOrder = null;
            await BrokerUiHost.Wait(async () => { brokerOrder = (await query.ListAsync(submitted.Id)).Value?.SingleOrDefault(); if (brokerOrder?.Status == BrokerOrderStatus.Rejected)
                    throw new InvalidOperationException($"Broker rejected order: {brokerOrder.DispatchCategory}: {brokerOrder.DispatchDetail}");
                return brokerOrder?.Status == BrokerOrderStatus.Working; });
            await BrokerUiHost.Wait(() => Task.FromResult(Find("brokerPreviewUpdateLimit").IsEnabled));
            Assert.True(Find("brokerPreviewCancelUnfilled").IsEnabled);
            await OnUi(()=> {
                var tabs = (TabControl)blotter.Controls.Find("tradeBlotterTabs",true).Single();
                Assert.Equal("Order Fills",tabs.SelectedTab!.Text);
                Assert.Equal(Color.Yellow,((TreeView)blotter.Controls.Find("orderFillsPreview",true).Single().Controls.Find("brokerPreviewOrderTree",true).Single()).Nodes[0].ForeColor);
            });
            var price = Find("brokerPreviewUpdateOrderPrice").FindFirstDescendant(cf=>cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)).AsTextBox();
            price.Text = (limit-.25m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Find("brokerPreviewUpdateLimit").AsButton().Invoke();
            await BrokerUiHost.Wait(async()=> (await query.GetAsync(brokerOrder!.Id)).Value?.CurrentSignedNetDebitLimit == limit-.25m);
            if (!fill) {
                await BrokerUiHost.Wait(()=>Task.FromResult(Find("brokerPreviewCancelUnfilled").IsEnabled));
                Find("brokerPreviewCancelUnfilled").AsButton().Invoke();
            } else if (!simulate) {
                foreach(var leg in submitted.Components[0].Legs)
                    await host.Broker.PublishMarketQuoteAsync(new(leg.ContractId,10m,10.05m,100,100,DateTime.UtcNow,1,1));
            }
            await BrokerUiHost.Wait(async()=> (await query.GetAsync(brokerOrder!.Id)).Value?.Status == (fill ? BrokerOrderStatus.Filled : BrokerOrderStatus.Cancelled));
            if (fill) {
                var databases = host.Host.Services.GetRequiredService<Container>().GetInstance<IDbContextFactory>();
                await BrokerUiHost.Wait(async()=> (await databases.TradeDb.GetTradeOrderAsync(submitted.Id))?.Status == TradeOrderStatus.Completed);
                await BrokerUiHost.Wait(() => Task.FromResult(notifiedTrades.ContainsKey(submitted.Id.OrderId)
                    && notifiedPositions.ContainsKey(submitted.Id.OrderId)));
            }
            await BrokerUiHost.Wait(()=>Task.FromResult(Find("orderExecutionNotificationStatus").Name.StartsWith(fill ? "Filled" : "Cancelled")));
            Assert.False(Find("brokerPreviewUpdateLimit").IsEnabled); Assert.False(Find("brokerPreviewCancelUnfilled").IsEnabled);
            await OnUi(()=> { var status = (Label)blotter.Controls.Find("orderExecutionNotificationStatus",true).Single();
                Assert.Equal(fill ? Color.LimeGreen : Color.Red,status.ForeColor);
                var tree = (TreeView)blotter.Controls.Find("orderFillsPreview",true).Single().Controls.Find("brokerPreviewOrderTree",true).Single();
                if(fill && !simulate) Assert.Equal(submitted.Components[0].Legs.Length,tree.Nodes[0].Nodes.Count);
                if(fill && simulate) Assert.Equal(0,tree.Nodes[0].Nodes.Count % submitted.Components[0].Legs.Length);
            });
        } catch(Exception error) {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"flaui-failures.log"),$"{type}/{fill}: {error}\n");
            throw;
        } finally {
            await publicationListener.StopAsync();
            await OnUi(()=>windowForm.Close()); ui.Join(TimeSpan.FromSeconds(5));
        }
    }
    // WinForms popup lists are separate HWNDs. Discover that window directly, then
    // use FlaUI's selection pattern; desktop traversal cannot locate these owned popups reliably.
    static void SelectCombo(FlaUI.Core.AutomationElements.AutomationElement element, string text, UIA3Automation automation)
    {
        Assert.True(element.IsEnabled);
        var combo = element.AsComboBox();
        combo.Expand();
        var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
        Assert.True(GetComboBoxInfo(new IntPtr(element.Properties.NativeWindowHandle.Value), ref info));
        var list = automation.FromHandle(info.List);
        var item = list.FindFirstDescendant(cf => cf.ByName(text))
            ?? throw new InvalidOperationException($"Combo item {text} was not exposed by its popup window.");
        item.Patterns.SelectionItem.Pattern.Select();
        combo.Collapse();
    }
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct ComboBoxInfo { public int Size; public NativeRect ItemRect, ButtonRect; public int ButtonState; public IntPtr Combo, Item, List; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetComboBoxInfo(IntPtr handle, ref ComboBoxInfo info);
    private sealed class UnloadedLegacy : Control,ITradeOrderControl {
        public DateOnly MaturityDate => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        public Task RemoveTradeAsync(int f,int o,int t)=>Task.CompletedTask;
        public Task<Guid> SubmitOrderAsync(DateOnly d,OrderActionType a,ITradeOrderConfirmationService c)=>throw new InvalidOperationException("Legacy submission must not run.");
        public Task SetLiveFeedAsync(bool e)=>Task.CompletedTask;
        public void SetNearestStrikePrices() { }
        public Task OrderActionTypeChangedAsync(OrderActionType a)=>Task.CompletedTask;
    }
}
