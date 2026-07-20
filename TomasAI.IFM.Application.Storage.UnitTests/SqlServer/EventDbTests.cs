using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventDb;
using TomasAI.IFM.Domain.Fund;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.Commands;
using TomasAI.IFM.Shared.Fund.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.MarketDataDb;

namespace TomasAI.IFM.Application.Storage.UnitTests.SqlServer
{
    public class EventDatabaseFixture : IDisposable
    {

        public EventDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                 .Add("EventDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=eventtestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, EventDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            DbFactory = new DbContextFactory(dbResolver);
            var dbCache = new DbCache();
            var logger = Substitute.For<ILogger<EventDbContext>>();
            logger.When(_ => { }).Do(_ => { });
            diContainer.Add(typeof(IObjectRepository<EventDbContext>), new EventDbContext(dbConn, DbFactory, dbCache, logger));
            Db = DbFactory.EventDb as EventDbContext;
        }
        public EventDbContext Db { get; }

        public IDbContextFactory DbFactory { get; }

        public void Dispose()
        {
        }
    }

    public class EventDbTests : IClassFixture<EventDatabaseFixture>
    {
        private readonly EventDatabaseFixture _testFixture;

        public EventDbTests(EventDatabaseFixture testFixture)
        {
            _testFixture = testFixture;
        }

        public async Task UpdateEventDataOk()
        {
            var db = _testFixture.Db;
            var eventLogs = await db.Use(@"select 
                                el.EventId,
                                el.EventSourceId,
                                el.EventSourceVersion,
                                et.EventTypeName,
                                el.EventTypeId,
                                el.EventData,
                                el.EventDate
                                from dbo.event_log el join dbo.event_type et on el.EventTypeId = et.EventTypeId
                                where el.EventTypeId = 32").ExecuteQueryAsync<EventLog>();
            foreach (var e in eventLogs)
            {
                var eventData = e.EventData;
                if (!string.IsNullOrWhiteSpace(eventData))
                {
                    eventData = eventData.Replace("SpreadTradeData", "TradePosition");
                    await db.InsertEventData(e.EventId, eventData);
                }
            }

        }

        public async Task UpdateEventSourceEntityTypeIdOk()
        {
            var db = _testFixture.Db;
            await db.UpdateEventSourceEntityTypeIdAsync();
        }

        public async Task UpdateEventLogEventTypeIdOk()
        {
            var db = _testFixture.Db;
            await db.UpdateEventLogEventTypeIdAsync();
        }

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
            await db.SaveEventsAsync(typeof(FundAggregateState), entityId, domainEvents, createfundCommand);
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

    }
}
