using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Xunit;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.Postgres.EventSourceDb;

public class EventSourceFixture : IDisposable
{

    public EventSourceFixture()
    {
        var dbConn = new DbConnectionSettings()
             .Add("EventSourceDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=event-source-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, EventSourceDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(_ => { 
            if (redisCacheMap.ContainsKey(_.ArgAt<string>(0)))
                _[1] = redisCacheMap[_.ArgAt<string>(0)];
            return redisCacheMap.ContainsKey(_.ArgAt<string>(0)); 
        });
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<EventSourceDbContext>), new EventSourceDbContext(dbConn, DbFactory, blackboardService, logger));
        Db = DbFactory.EventSourceDb as EventSourceDbContext;
    }
    public EventSourceDbContext Db { get; }

    public IDbContextFactory DbFactory { get; }

    public void Dispose()
    {
    }
}

public class EventSourceDbTests : IClassFixture<EventSourceFixture>
{
    private readonly EventSourceFixture _testFixture;

    public EventSourceDbTests(EventSourceFixture testFixture)
    {
        _testFixture = testFixture;
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
         db.Use($"delete from event_log ").ExecuteCommandAsync().Wait();
    }

    [Fact]
    public async Task InsertCommandLogAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        var createFundCmd = new CreateFundCommand( ScyllaDb.FundDb.SampleData.Fund)
        ;
        await db.Use($"delete from command_log").ExecuteCommandAsync();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true, // For pretty printing
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var commandData = JsonSerializer.Serialize(createFundCmd, options);
        var rowCount = await  db.InsertCommandLogAsync(createFundCmd, DateTime.Now, commandData);
        rowCount.Should().Be(1);
        var commandLog = await db.GetCommandLogAsync(createFundCmd.CommandId);
        commandLog.Should().NotBeNull();
        commandLog.CommandId.Should().Be(createFundCmd.CommandId);
        commandLog.StreamId.Should().Be(createFundCmd.StreamId);
        commandLog.AggregateName.Should().Be(createFundCmd.RouteTo);
        commandLog.CommandName.Should().Be(createFundCmd.CommandName);
        commandLog.CommandData.Should().Be(commandData);
    }

    
     [Fact]
    public async Task GetEventStreamIdAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        var eventStream = "TestStreamId";
        var eventStreamId = await db.GetEventStreamIdAsync(eventStream);
        eventStreamId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetEventStreamIdFromDbAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        var eventStream = "TestStreamId";
        _ = await db.GetEventStreamIdAsync(eventStream);
        var eventStreamId = await db.GetEventStreamIdFromDbAsync(eventStream);
        eventStreamId.Should().NotBeNull();
        eventStreamId.IsValid.Should().BeTrue();
        eventStreamId.EventStream.Should().Be(eventStream);
    }

    [Fact]
    public async Task GetEventNameIdAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        var eventNameId = await db.GetEventNameIdFromTypeAsync<FundMaxProfitGeneratedEvent>();
        eventNameId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetEventNameIdFromDbAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        var testEventNameId = await db.GetEventNameIdFromTypeAsync<FundBalanceChangedEvent>();
        var eventName = typeof(FundBalanceChangedEvent).Name;
        var eventFullName = typeof(FundBalanceChangedEvent).FullName;
        var eventNameId = await db.GetEventNameIdFromDbAsync(eventName);
        eventNameId.Should().NotBeNull();
        eventNameId.IsValid.Should().BeTrue();
           eventNameId.EventName.Should().Be(eventName);
        eventNameId.EventTypeName.Should().Be(eventFullName);
    }

    [Fact]
    public async Task GetEventsByEventStreamIdAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        var eventNameId = await db.GetEventNameIdFromTypeAsync<FundCreatedEvent>();
        eventNameId.Should().BeGreaterThan(0);
        var eventStream = "TestStreamId";
        var eventStreamId = await db.GetEventStreamIdAsync(eventStream);
        eventStreamId.Should().BeGreaterThan(0);
        var createFundCmd = new CreateFundCommand( ScyllaDb.FundDb.SampleData.Fund);

        // Create a FundCreatedEvent with the sample FundReadModel
        var fundCreatedEvent = new FundCreatedEvent
        {
            NewFund = ScyllaDb.FundDb.SampleData.Fund
        };
        fundCreatedEvent.RoutedFrom(createFundCmd);

        var eventData = JsonSerializer.Serialize(fundCreatedEvent, new JsonSerializerOptions
        {
            WriteIndented = false, 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var eventDate = DateTime.Now;
        var eventVersion = await db.InsertEventLogAsync(eventStreamId, eventNameId, eventData, createFundCmd.CommandId, eventDate);
      //var events =  await db.GetEventStreamAsync(eventStreamId);
        //events.Count.Should().BeGreaterThan(0);
        //events.Any(e => e.EventTypeName.Contains(typeof(FundCreatedEvent).Name)).Should().BeTrue();
    }

    [Fact]
    public async Task GetEventsLastNRangeAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
  
        var createFundCmd = new CreateFundCommand( ScyllaDb.FundDb.SampleData.Fund);

        // Create a FundCreatedEvent with the sample FundReadModel
        var fundCreatedEvent = new FundCreatedEvent();
        EventInitHelper.SetProperty(fundCreatedEvent, nameof(FundCreatedEvent.NewFund), ScyllaDb.FundDb.SampleData.Fund);
        fundCreatedEvent.RoutedFrom(createFundCmd);

        var fundOrder = new FundOrderReadModel(
        fundId: 1001,
        orderId: 2001,
        orderDate: DateTime.Now,
        orderStatus: Domain.Fund.Shared.OrderStatus.Open,
        baseContractId: "BaseContract123",
        tradeDate: DateOnly.FromDateTime(DateTime.Now.Date),
        maturityDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(1).Date),
        reference: "Sample Reference",
        createdOn: DateTime.Now,
        createdBy: "admin",
        updatedOn: null,
        updatedBy: null
    );

        // Add a sample FundOrderTradeReadModel to the FundOrder
        var fundOrderTrade = new FundOrderTradeReadModel(
            fundId: 1001,
            orderId: 2001,
            tradeId: 3001,
            tradeType: TradeType.LongCall,
            tradeDate: DateOnly.FromDateTime(DateTime.Now.Date),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(1).Date),
            tradeState: TradeState.NewTrade,
            tradeAction: TradeAction.Buy,
            reference: "Trade Reference",
            primaryTrade: true,
            baseContractSymbol: "BaseContractSymbol",
            createdOn: DateTime.Now,
            createdBy: "admin",
            updatedOn: DateTime.Now,
            updatedBy: "admin"
        );

        fundOrder.Add(fundOrderTrade);

        // Create a sample AddOrderToFundCommand
        var addOrderToFundCommand = new AddOrderToFundCommand( fundOrder);

        var orderAddedToFundEvent1 = new OrderAddedToFundEvent
        {
            FundOrder = fundOrder
        };
        orderAddedToFundEvent1.RoutedFrom(addOrderToFundCommand);

        var orderAddedToFundEvent2 = new OrderAddedToFundEvent
        {
            FundOrder = orderAddedToFundEvent1.FundOrder with { OrderId = 2002 }
        };
        orderAddedToFundEvent2.RoutedFrom(addOrderToFundCommand);

        var orderAddedToFundEvent3 = new OrderAddedToFundEvent
        {
            FundOrder = orderAddedToFundEvent1.FundOrder with { OrderId = 2003 }
        };
        orderAddedToFundEvent3.RoutedFrom(addOrderToFundCommand);

        var domainEvents = new DomainEventCollection([fundCreatedEvent, orderAddedToFundEvent1, orderAddedToFundEvent2, orderAddedToFundEvent3]);
        var savedEvents = await db.SaveEventsAsync( createFundCmd.StreamId, createFundCmd.CommandId, domainEvents, async (e) => await Task.CompletedTask);
        var eventStreamId = await db.GetEventStreamIdAsync(createFundCmd.StreamId);
       var lastNEvents = await db.GetEventsLastNRangeAsync(eventStreamId, 3);
        lastNEvents.Count.Should().Be(3);
        lastNEvents.Any(e => e.EventTypeName == typeof(FundCreatedEvent).FullName).Should().BeFalse();
        lastNEvents.Any(e => e.EventTypeName == typeof(OrderAddedToFundEvent).FullName).Should().BeTrue();
    }

    [Fact]
    public async Task GetEventsFromSnapshotAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;

        var createFundCmd = new CreateFundCommand(ScyllaDb.FundDb.SampleData.Fund);

        // Create a FundCreatedEvent with the sample FundReadModel
        var fundCreatedEvent = new FundCreatedEvent();
        EventInitHelper.SetProperty(fundCreatedEvent, nameof(FundCreatedEvent.NewFund), ScyllaDb.FundDb.SampleData.Fund);
        fundCreatedEvent.RoutedFrom(createFundCmd);

        var fundOrder = new FundOrderReadModel(
            fundId: 1001,
            orderId: 2001,
            orderDate: DateTime.Now,
            orderStatus: Domain.Fund.Shared.OrderStatus.Open,
            baseContractId: "BaseContract123",
            tradeDate: DateOnly.FromDateTime(DateTime.Now.Date),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(1).Date),
            reference: "Sample Reference",
            createdOn: DateTime.Now,
            createdBy: "admin",
            updatedOn: null,
            updatedBy: null
        );

        // Add a sample FundOrderTradeReadModel to the FundOrder
        var fundOrderTrade = new FundOrderTradeReadModel(
            fundId: 1001,
            orderId: 2001,
            tradeId: 3001,
            tradeType: TradeType.LongCall,
            tradeDate: DateOnly.FromDateTime(DateTime.Now.Date),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddMonths(1).Date),
            tradeState: TradeState.NewTrade,
            tradeAction: TradeAction.Buy,
            reference: "Trade Reference",
            primaryTrade: true,
            baseContractSymbol: "BaseContractSymbol",
            createdOn: DateTime.Now,
            createdBy: "admin",
            updatedOn: DateTime.Now,
            updatedBy: "admin"
        );

        fundOrder.Add(fundOrderTrade);

        // Create a sample AddOrderToFundCommand
        var addOrderToFundCommand = new AddOrderToFundCommand( fundOrder);

        var orderRemovedFromFundEvent1 = new OrderRemovedFromFundEvent
        {
            FundOrderId = FundOrderId.Create(fundOrder.FundId, fundOrder.OrderId)
        };
        orderRemovedFromFundEvent1.RoutedFrom(addOrderToFundCommand);

        var orderAddedToFundEvent1 = new OrderAddedToFundEvent
        {
            FundOrder = fundOrder
        };
        orderAddedToFundEvent1.RoutedFrom(addOrderToFundCommand);

        var orderAddedToFundEvent2 = new OrderAddedToFundEvent
        {
            FundOrder = orderAddedToFundEvent1.FundOrder with { OrderId = 2002 }
        };
        orderAddedToFundEvent2.RoutedFrom(addOrderToFundCommand);

        var orderAddedToFundEvent3 = new OrderAddedToFundEvent
        {
            FundOrder = orderAddedToFundEvent1.FundOrder with { OrderId = 2003 }
        };
        orderAddedToFundEvent3.RoutedFrom(addOrderToFundCommand);

        var domainEvents = new DomainEventCollection( [fundCreatedEvent, orderAddedToFundEvent1, orderRemovedFromFundEvent1, orderAddedToFundEvent2, orderAddedToFundEvent3]);
        var savedEvents = await db.SaveEventsAsync(createFundCmd.StreamId, createFundCmd.CommandId, domainEvents, async (e) => await Task.CompletedTask);
        var eventStreamId = await db.GetEventStreamIdAsync(createFundCmd.StreamId);
        var snapshotEvents = await db.GetEventsFromSnapshotAsync< OrderRemovedFromFundEvent>(eventStreamId);
        snapshotEvents.Count.Should().Be(3);
        snapshotEvents.Any(e => e.EventTypeName == typeof(FundCreatedEvent).FullName).Should().BeFalse();
        snapshotEvents.Any(e => e.EventTypeName == typeof(OrderRemovedFromFundEvent).FullName).Should().BeTrue();
        snapshotEvents.Any(e => e.EventTypeName == typeof(OrderAddedToFundEvent).FullName).Should().BeTrue();
    }

    [Fact]
    public async Task SaveEventsAsyncOk()
    {
        var db = _testFixture.DbFactory.EventSourceDb as EventSourceDbContext;
        await db.Use($"delete from event_log ").ExecuteCommandAsync();

        // Create a sample FundTransactionReadModel
        var fundTransaction = new FundTransactionReadModel(
            transactionId: 0,
            transactionDate: DateTime.Now,
            transactionType: FundTransactionType.CashDeposit,
            fundId: 1001,
            orderId: 2001,
            tradeId: 3001,
            tradeType: TradeType.LongCall,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeStatus: TradeStatus.Open,
            description: "Sample fund deposit transaction",
            amount: 10000.00m,
            balance: 50000.00m
        );

        var createFundTransactionCmd = new CreateFundTransactionCommand( fundTransaction);

        var createFundCmd = new CreateFundCommand(ScyllaDb.FundDb.SampleData.Fund);

        // Create a FundCreatedEvent with the sample FundReadModel
        var fundCreatedEvent = new FundCreatedEvent
        {
            NewFund = ScyllaDb.FundDb.SampleData.Fund
        };
        fundCreatedEvent.RoutedFrom(createFundCmd);

        // Create a FundTransactionEvent with the sample FundTransactionReadModel
        var fundTransactionEvent = new FundTransactionEvent
        {
            FundTransaction = fundTransaction,
            CreatedBy = "admin",
            CreatedOn = DateTime.Now
        };
        fundTransactionEvent.RoutedFrom(createFundTransactionCmd);

        var domainEvents = new DomainEventCollection([fundCreatedEvent, fundTransactionEvent ]); 
        var savedEvents = await db.SaveEventsAsync( createFundCmd.StreamId, createFundCmd.CommandId, domainEvents, async (e) => await Task.CompletedTask);
        savedEvents.Count.Should().BeGreaterThan(0);
        savedEvents.Any(e => e.EventName == typeof(FundCreatedEvent).Name).Should().BeTrue();
        savedEvents.Any(e => e.EventName == typeof(FundTransactionEvent).Name).Should().BeTrue();
    }

    
   
    /*
    [Fact]
    public async Task GetEntityIdOk()
    {
        var db = _testFixture.Db;
        var entityIdValue = "1234";
        var rowCount = await db.Use($"select count(*) from entity_id where Value = '{entityIdValue}'").ExecuteScalarAsync<int>();
        rowCount.Should().Be(0);
        var entityId = await db.GetEntityIdAsync(entityIdValue);
        rowCount = await db.Use($"select count(*) from entity_id where EntityId = {entityId}").ExecuteScalarAsync<int>();
        rowCount.Should().Be(1);
        await db.Use($"delete from entity_id where Value = '{entityIdValue}'").ExecuteCommandAsync();
    }

    [Fact]
    public async Task SaveEventsOk()
    {
        var db = _testFixture.Db;
        var newFund = new FundReadModel
        (
            FundId: 1001,
            Name: "TestFund",
            Description: "A fund for testing purposes only",
            Balance: 1000.00m,
            IsProduction: false,
            CreatedOn: DateTime.Now,
            CreatedBy: "basilt"
        );
        var createfundCommand = new CreateFundCommand
        {
            NewFund = newFund,
            PostEvents = false
        };
        var fundCreatedEvent = new FundCreatedEvent
        { 
            CommandId = createfundCommand.CommandId,
            NewFund = newFund
        };

        var entityId = await db.GetEntityIdAsync($"{newFund.FundId}");
        var domainEvents = new DomainEventCollection(1, new IEvent[] { fundCreatedEvent });
        await db.SaveEventsAsync(typeof(FundboundedContextState), entityId, domainEvents, createfundCommand);
        var entityTypeName = typeof(FundAggregate).FullName;
        var entityTypeId = db.GetEntityTypeId(entityTypeName, 
            e => db.Use($"exec spGetEntityTypeId @entityTypeName = '{e}'")
                 .ExecuteScalarAsync<long>().Result);
        var eventLog = await db
            .Use($"exec	spGetEventLog @entityId = {entityId}, @entityTypeId = {entityTypeId}")
            .ExecuteQueryAsync<EventLog>();
        eventLog.Should().NotBeNull();
        eventLog.Count.Should().BeGreaterThan(0);
        var eventId = eventLog.ElementAt(0).EventId;
        eventLog.ElementAt(0).ToDomainEvent(eventId).Should().BeOfType<FundCreatedEvent>();
        var domainEvent = eventLog.ElementAt(0).ToDomainEvent(eventId) as FundCreatedEvent;
        domainEvent.Should().NotBeNull();
        fundCreatedEvent.NewFund.FundId.Should().Be(domainEvent.NewFund.FundId);
        fundCreatedEvent.NewFund.Name.Should().Be(domainEvent.NewFund.Name);
        fundCreatedEvent.NewFund.Description.Should().Be(domainEvent.NewFund.Description);
        fundCreatedEvent.NewFund.Balance.Should().Be(domainEvent.NewFund.Balance);
        $"{fundCreatedEvent.NewFund.CreatedOn:yyyy-MM-dd}".Should().Be($"{domainEvent.NewFund.CreatedOn:yyyy-MM-dd}");
        fundCreatedEvent.NewFund.CreatedBy.Should().Be(domainEvent.NewFund.CreatedBy);

        await db.Use($"delete from event_log where EventId = {eventLog.ElementAt(0).EventId}").ExecuteCommandAsync();
        await db.Use($"delete from event_source where EntityTypeId = {entityTypeId}").ExecuteCommandAsync();
    }
*/
}
