using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Domain.MarketData.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public class MarketDataQueryActorTests : IClassFixture<MarketDataTestFixture>
{
    readonly MarketDataTestFixture _fixture;

    public MarketDataQueryActorTests(MarketDataTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Test helper to expose protected ParseMessage for unit testing.
    public class TestableMarketDataQueryActor : MarketDataQueryActor
    {
        public TestableMarketDataQueryActor(IDbContextFactory dbFactory, ILogger<MarketDataQueryActor> logger)
            : base(dbFactory, logger)
        {
        }

        public IQuery InvokeParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IQueryActorContext context, IQuery query)
            => await ReceiveAsync(context, query);

        public async ValueTask InvokeOnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
            => await OnExceptionAsync(context, threadId, query, verb, ex);

    }

    #region ParseMessage Happy Path Tests

    [Fact]
    public void ParseMessage_ShouldParseGetLastRateOfReturnQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}"),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetLastRateOfReturnQuery>();
        var parsedQuery = result as GetLastRateOfReturnQuery;
        parsedQuery!.Subject.Should().BeEquivalentTo(query.Subject);
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetLastRateOfReturnQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetTradingDaysQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetTradingDaysParameter(SampleData.StartDate, SampleData.EndDate, SampleData.Market, SampleData.Currency);
        var query = new GetTradingDaysQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDaysQuery.Actor, GetTradingDaysQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetTradingDaysQuery>();
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradingDaysQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetTradingDatesQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetTradingDatesParameter(SampleData.StartDate, SampleData.EndDate, SampleData.Market, SampleData.Currency);
        var query = new GetTradingDatesQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDatesQuery.Actor, GetTradingDatesQuery.Verb, entityId.Format()),
            EntityId = entityId
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetTradingDatesQuery>();
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetTradingDatesQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldParseGetValueDateQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var query = new GetValueDateQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetValueDateQuery.Actor, GetValueDateQuery.Verb, new GetValueDateParameter().Format()),
            EntityId = new GetValueDateParameter()
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetValueDateQuery>();
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is(GetValueDateQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ParseMessage Edge Case Tests

    [Fact]
    public void ParseMessage_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}"),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(null!, message);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorTypeIsNotQuery()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Command, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}");
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve MarketDataQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenActorNameIsIncorrect()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Query, "WrongActor", GetLastRateOfReturnQuery.Verb, $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}");
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve MarketDataQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenVerbIsNotInParseMap()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var invalidSubject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, "UnknownVerb", $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}");
        var message = new NatsMsg<byte[]>
        {
            Subject = invalidSubject.ToString(),
            Data = Array.Empty<byte>()
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unable to resolve MarketDataQuery query from message: *");
    }

    [Fact]
    public void ParseMessage_ShouldThrowInvalidOperationException_WhenDeserializedQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var validSubject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}");
        var message = new NatsMsg<byte[]>
        {
            Subject = validSubject.ToString(),
            Data = Array.Empty<byte>() // Empty data will result in null deserialization
        };

        // Act & Assert
        var act = () => actor.InvokeParseMessage(context, message);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseMessage_ShouldHandleEmptyEntityId_InSubject()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var subjectWithEmptyEntityId = new ActorSubject(ActorType.Query, GetValueDateQuery.Actor, GetValueDateQuery.Verb, string.Empty);
        var query = new GetValueDateQuery
        {
            Subject = subjectWithEmptyEntityId,
            EntityId = new GetValueDateParameter()
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = subjectWithEmptyEntityId.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GetValueDateQuery>();
    }

    [Fact]
    public void ParseMessage_ShouldExtractThreadIdFromSubject_Correctly()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        result.Should().NotBeNull();
        context.Received(1).SetMessageInfo(
            Arg.Is<ActorThreadId>(tid => tid == query.Subject.ThreadId),
            Arg.Is(GetLastRateOfReturnQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    [Fact]
    public void ParseMessage_ShouldSetMessageInfoCorrectly_WithAllComponents()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var serializedData = _fixture.DataSerializer.Serialize(query);
        var message = new NatsMsg<byte[]>
        {
            Subject = query.Subject.ToString(),
            Data = serializedData
        };

        // Act
        var result = actor.InvokeParseMessage(context, message);

        // Assert
        context.Received(1).SetMessageInfo(
            Arg.Any<ActorThreadId>(),
            Arg.Is<string>(v => v == GetLastRateOfReturnQuery.Verb),
            Arg.Any<ActorMessageInfo>());
    }

    #endregion

    #region ReceiveAsync Happy Path Tests

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetLastRateOfReturnQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var rateOfReturn = SampleData.RateOfReturn;
        var marketDataDbContext = Substitute.For<IMarketDataDbContext>();
        marketDataDbContext.GetLastRateOfReturnAsync(Arg.Any<string>())
            .Returns(ci => Task.FromResult(rateOfReturn));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb
            .Returns(ci => marketDataDbContext);

        var actor = _fixture.CreateActor(logger, dbFactory);

        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate),
            Symbol = SampleData.Symbol,
            ValueDate = SampleData.ValueDate
        };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
               Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
               Arg.Is<string>(v => v == GetLastRateOfReturnQuery.Verb),
               Arg.Is<ServiceResult<RateOfReturnReadModel?>>(r => r.Success 
                && r.Value != null 
                && r.Value.Symbol == SampleData.Symbol 
                && r.Value.ValueDate == SampleData.ValueDate
                && r.Value.RateOfReturn == SampleData.RateOfReturn.RateOfReturn)
           );
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradingDaysQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var tradingDates = SampleData.TradingDates;
        var marketDataDbContext = Substitute.For<IMarketDataDbContext>();
        marketDataDbContext.GetTradingDatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Shared.MarketData.MarketType>(), Arg.Any<Shared.MarketData.CurrencyType>())
            .Returns(ci => Task.FromResult(tradingDates));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb
            .Returns(ci => marketDataDbContext);
        var actor = _fixture.CreateActor(logger, dbFactory);

        var entityId = new GetTradingDaysParameter(SampleData.StartDate, SampleData.EndDate, SampleData.Market, SampleData.Currency);
        var query = new GetTradingDaysQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDaysQuery.Actor, GetTradingDaysQuery.Verb, entityId.Format()),
            EntityId = entityId,
            StartDate = SampleData.StartDate,
            EndDate = SampleData.EndDate,
            MarketType = SampleData.Market,
            CurrencyType = SampleData.Currency
        };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
               Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
               Arg.Is<string>(v => v == GetTradingDaysQuery.Verb),
               Arg.Is<ServiceResult<ScalarReadModel<int>>>(r => r.Success
                && r.Value != null
                && r.Value.Value == SampleData.TradingDates.Length)
           );
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetTradingDatesQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var tradingDates = SampleData.TradingDates;
        var marketDataDbContext = Substitute.For<IMarketDataDbContext>();
        marketDataDbContext.GetTradingDatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Shared.MarketData.MarketType>(), Arg.Any<Shared.MarketData.CurrencyType>())
            .Returns(ci => Task.FromResult(tradingDates));
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.MarketDataDb
            .Returns(ci => marketDataDbContext);
        var actor = _fixture.CreateActor(logger, dbFactory);

        var entityId = new GetTradingDatesParameter(SampleData.StartDate, SampleData.EndDate, SampleData.Market, SampleData.Currency);
        var query = new GetTradingDatesQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDatesQuery.Actor, GetTradingDatesQuery.Verb, entityId.Format()),
            EntityId = entityId,
            StartDate = SampleData.StartDate,
            EndDate = SampleData.EndDate,
            MarketType = SampleData.Market,
            CurrencyType = SampleData.Currency
        };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
               Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
               Arg.Is<string>(v => v == GetTradingDatesQuery.Verb),
               Arg.Is<ServiceResult<DateOnly[]>>(r => r.Success
                && r.Value != null
                && r.Value.Length == SampleData.TradingDates.Length
                && r.Value[0] == SampleData.TradingDates[0])
           );
    }

    [Fact]
    public async Task ReceiveAsync_ShouldProcessGetValueDateQuery_Successfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var actor = _fixture.CreateActor(logger, dbFactory);

        var entityId = new GetValueDateParameter();
        var query = new GetValueDateQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetValueDateQuery.Actor, GetValueDateQuery.Verb, entityId.Format()),
            EntityId = entityId
        };

        var context = Substitute.For<IQueryActorContext>();
        context.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>()).Returns(true);

        // Act
        await actor.InvokeReceiveAsync(context, query);

        // Assert
        await context.Received(1).ReplyAsync(
               Arg.Is<ActorThreadId>(id => id == query.Subject.ThreadId),
               Arg.Is<string>(v => v == GetValueDateQuery.Verb),
               Arg.Is<ServiceResult<ScalarReadModel<DateOnly>>>(r => r.Success
                && r.Value != null
                && r.Value.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
           );
    }

    #endregion

    #region ReceiveAsync Edge Case Tests

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var db = Substitute.For<IMarketDataDbContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(null!, query);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

   
    [Fact]
    public async Task ReceiveAsync_ShouldThrowInvalidOperationException_WhenQueryTypeIsNotSupported()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var db = Substitute.For<IMarketDataDbContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";

        // Create an unsupported query type
        var unsupportedQuery = Substitute.For<IQuery>();
        unsupportedQuery.Subject.Returns(new ActorSubject(ActorType.Query, MarketDataQueryActor.ActorName, "UnsupportedVerb", entityFormat));

        // Act & Assert
        var act = async () => await actor.InvokeReceiveAsync(context, unsupportedQuery);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to process MarketDataQuery query: *");
    }

    #endregion

    #region OnExceptionAsync Happy Path Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetLastRateOfReturnQuery_Exception()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate),
            ErrorCode = 500
        };
        var exception = new Exception("Database connection failed");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastRateOfReturnQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastRateOfReturnQuery.Verb,
            Arg.Is<ServiceResult<RateOfReturnReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 500 &&
                r.ErrorMessage == "Database connection failed"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradingDaysQuery_Exception()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetTradingDaysParameter(SampleData.StartDate, SampleData.EndDate, SampleData.Market, SampleData.Currency);
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityId.Format());
        var query = new GetTradingDaysQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDaysQuery.Actor, GetTradingDaysQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = 404
        };
        var exception = new Exception("Trading days not found");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradingDaysQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradingDaysQuery.Verb,
            Arg.Is<ServiceResult<ScalarReadModel<int>>>(r =>
                !r.Success &&
                r.ErrorCode == 404 &&
                r.ErrorMessage == "Trading days not found"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetTradingDatesQuery_Exception()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetTradingDatesParameter(SampleData.StartDate, SampleData.EndDate, SampleData.Market, SampleData.Currency);
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityId.Format());
        var query = new GetTradingDatesQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetTradingDatesQuery.Actor, GetTradingDatesQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = 400
        };
        var exception = new Exception("Invalid date range");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetTradingDatesQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetTradingDatesQuery.Verb,
            Arg.Is<ServiceResult<DateOnly[]>>(r =>
                !r.Success &&
                r.ErrorCode == 400 &&
                r.ErrorMessage == "Invalid date range"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleGetValueDateQuery_Exception()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityId = new GetValueDateParameter();
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityId.Format());
        var query = new GetValueDateQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetValueDateQuery.Actor, GetValueDateQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = 503
        };
        var exception = new Exception("Service unavailable");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetValueDateQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetValueDateQuery.Verb,
            Arg.Is<ServiceResult<ScalarReadModel<DateOnly>>>(r =>
                !r.Success &&
                r.ErrorCode == 503 &&
                r.ErrorMessage == "Service unavailable"));
    }

    #endregion

    #region OnExceptionAsync Edge Case Tests

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(null!, threadId, query, GetLastRateOfReturnQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenThreadIdIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, default, query, GetLastRateOfReturnQuery.Verb, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("threadId");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, null!, GetLastRateOfReturnQuery.Verb, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldThrowArgumentNullException_WhenVerbIsNull()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate)
        };
        var exception = new Exception("Test exception");

        // Act & Assert
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, null!, exception);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("verb");
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleUnknownQueryType_WithDefaultResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);

        // Create an unknown query type
        var unknownQuery = Substitute.For<IQuery>();
        unknownQuery.Subject.Returns(new ActorSubject(ActorType.Query, MarketDataQueryActor.ActorName, "UnknownVerb", entityFormat));
        unknownQuery.ErrorCode.Returns(9999);

        var exception = new Exception("Unknown query type");

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, unknownQuery, "UnknownVerb", exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            "UnknownVerb",
            Arg.Is<ServiceFailed<ActorEntityId>>(r =>
                !r.Success &&
                r.ErrorCode == 9999 &&
                r.ErrorMessage == "Unknown query type"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleNestedExceptions()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate),
            ErrorCode = 500
        };
        var innerException = new InvalidOperationException("Inner error");
        var exception = new Exception("Outer error", innerException);

        // Act
        await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastRateOfReturnQuery.Verb, exception);

        // Assert
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastRateOfReturnQuery.Verb,
            Arg.Is<ServiceResult<RateOfReturnReadModel?>>(r =>
                !r.Success &&
                r.ErrorCode == 500 &&
                r.ErrorMessage == "Outer error"));
    }

    [Fact]
    public async Task OnExceptionAsync_ShouldHandleExceptionWhenReplyAsyncThrows()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MarketDataQueryActor>>();
        var actor = _fixture.CreateActor(logger);
        var context = Substitute.For<IQueryActorContext>();
        var entityFormat = $"{SampleData.Symbol}.{SampleData.ValueDate:yyyy-MM-dd}";
        var threadId = new ActorThreadId(ActorType.Query, MarketDataQueryActor.ActorName, entityFormat);
        var query = new GetLastRateOfReturnQuery
        {
            Subject = new ActorSubject(ActorType.Query, GetLastRateOfReturnQuery.Actor, GetLastRateOfReturnQuery.Verb, entityFormat),
            EntityId = new GetLastRateOfReturnParameter(SampleData.Symbol, SampleData.ValueDate),
            ErrorCode = 500
        };
        var exception = new Exception("Original error");

        context.ReplyAsync(threadId, GetLastRateOfReturnQuery.Verb, Arg.Any<ServiceResult<RateOfReturnReadModel?>>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("Reply failed"));

        // Act - should not throw, should be caught and logged
        var act = async () => await actor.InvokeOnExceptionAsync(context, threadId, query, GetLastRateOfReturnQuery.Verb, exception);
        await act.Should().NotThrowAsync();

        // Assert - verify ReplyAsync was called (and it threw, which was caught)
        await context.Received(1).ReplyAsync(
            threadId,
            GetLastRateOfReturnQuery.Verb,
            Arg.Any<ServiceResult<RateOfReturnReadModel?>>());
    }

    #endregion

}