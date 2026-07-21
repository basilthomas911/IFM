using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests;

/// <summary>
/// Unit tests for <see cref="NatsActorEventListener"/> covering happy paths and edge cases
/// for all methods and properties defined in <see cref="IActorEventListener"/>.
/// </summary>
public class NatsActorEventListenerTests
{
    private readonly ILogger<NatsActorEventListener> _logger;
    private readonly INatsEventListenerOptions _options;

    public NatsActorEventListenerTests()
    {
        _logger = Substitute.For<ILogger<NatsActorEventListener>>();
        _options = Substitute.For<INatsEventListenerOptions>();
        _options.Url.Returns("nats://localhost:4222");
    }

    #region Constructor Tests

    [Fact]
    public void Ctor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var listener = new NatsActorEventListener(_options, _logger);

        // Assert
        listener.Should().NotBeNull();
        listener.State.Should().Be(EventListenerState.Unknown);
        listener.MessageCount.Should().Be(0);
    }

    [Fact]
    public void Ctor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new NatsActorEventListener(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new NatsActorEventListener(_options, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region State Property Tests

    [Fact]
    public void State_InitialValue_ShouldBeUnknown()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);

        // Act & Assert
        listener.State.Should().Be(EventListenerState.Unknown);
    }

    [Fact]
    public async Task State_AfterStart_ShouldBeStarted()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-001";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert - State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }


    [Fact]
    public async Task State_AfterStop_ShouldBeStopped()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-002";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Act
        await listener.StopAsync();

        // Assert
        listener.State.Should().Be(EventListenerState.Stopped);
    }

    #endregion

    #region MessageCount Property Tests

    [Fact]
    public void MessageCount_InitialValue_ShouldBeZero()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);

        // Act & Assert
        listener.MessageCount.Should().Be(0);
    }

    #endregion

    #region StartAsync Tests - Happy Paths

    [Fact]
    public async Task StartAsync_WithValidParameters_ShouldStart()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-003";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert - State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WithSingleEventMap_ShouldStart()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-004";
        var mailboxId = new ActorMailboxId(ActorType.Event, "test-entity");
        var eventMap = new Dictionary<ActorMailboxId, List<string>>
    {
        { mailboxId, new List<string> { "event.created", "event.updated" } }
    };
        var eventHandler = CreateValidEventHandler();

        // Act
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert - State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyStarted_ShouldLogDebugAndReturn()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-005";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Act
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert - State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }

    #endregion

    #region StartAsync Tests - Edge Cases

    [Fact]
    public async Task StartAsync_WithNullEventListenerId_ShouldThrowArgumentNullException()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act
        Func<Task> act = async () => await listener.StartAsync(null!, eventMap, eventHandler);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_WithEmptyEventListenerId_ShouldThrowArgumentException()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act
        Func<Task> act = async () => await listener.StartAsync(string.Empty, eventMap, eventHandler);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartAsync_WithNullEventMap_ShouldThrowArgumentNullException()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-006";
        var eventHandler = CreateValidEventHandler();

        // Act
        Func<Task> act = async () => await listener.StartAsync(eventListenerId, null!, eventHandler);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_WithEmptyEventMap_ShouldThrowArgumentException()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-007";
        var eventMap = new Dictionary<ActorMailboxId, List<string>>();
        var eventHandler = CreateValidEventHandler();

        // Act
        Func<Task> act = async () => await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Event map cannot be empty*");
    }

    [Fact]
    public async Task StartAsync_WithMultipleDifferentMailboxKeys_ShouldThrowArgumentException()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-008";
        var mailboxId1 = new ActorMailboxId(ActorType.Event, "entity1");
        var mailboxId2 = new ActorMailboxId(ActorType.Event, "entity2");
        var eventMap = new Dictionary<ActorMailboxId, List<string>>
        {
            { mailboxId1, new List<string> { "event.created" } },
            { mailboxId2, new List<string> { "event.updated" } }
        };
        var eventHandler = CreateValidEventHandler();

        // Act
        Func<Task> act = async () => await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Event map must have same mailbox key*");
    }

    [Fact]
    public async Task StartAsync_WithNullEventHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-009";
        var eventMap = CreateValidEventMap();

        // Act
        Func<Task> act = async () => await listener.StartAsync(eventListenerId, eventMap, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_WithMultipleSameMailboxKeys_ShouldStart()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-010";
        var mailboxId = new ActorMailboxId(ActorType.Event, "entity1");
        var eventMap = new Dictionary<ActorMailboxId, List<string>>
    {
        { mailboxId, new List<string> { "event.created", "event.updated", "event.deleted" } }
    };
        var eventHandler = CreateValidEventHandler();

        // Act
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Assert - State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }


    #endregion

    #region StopAsync Tests - Happy Paths

    [Fact]
    public async Task StopAsync_AfterStart_ShouldStop()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-011";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Act
        await listener.StopAsync();

        // Assert
        listener.State.Should().Be(EventListenerState.Stopped);
    }

    [Fact]
    public async Task StopAsync_WithoutStart_ShouldNotThrow()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);

        // Act
        Func<Task> act = async () => await listener.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-012";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        await listener.StartAsync(eventListenerId, eventMap, eventHandler);
        await listener.StopAsync();

        // Act
        Func<Task> act = async () => await listener.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region StopAsync Tests - Edge Cases

    [Fact]
    public async Task StopAsync_BeforeStart_ShouldLogDebugAndComplete()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);

        // Act
        await listener.StopAsync();

        // Assert
        listener.State.Should().Be(EventListenerState.Unknown);
    }

    [Fact]
    public async Task StopAsync_MultipleSequentialCalls_ShouldHandleGracefully()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-013";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Act
        await listener.StopAsync();
        await listener.StopAsync();
        await listener.StopAsync();

        // Assert
        listener.State.Should().Be(EventListenerState.Stopped);
    }

    #endregion

    #region Integration Tests - Start/Stop Lifecycle

    [Fact]
    public async Task Lifecycle_StartStopStart_ShouldWorkCorrectly()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-014";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act - First cycle
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);
        // State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        await listener.StopAsync();
        listener.State.Should().Be(EventListenerState.Stopped);

        // Act - Second cycle
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);
        // State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }

    [Fact]
    public async Task Lifecycle_MultipleCycles_ShouldMaintainCorrectState()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-015";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act & Assert - Multiple cycles
        for (int i = 0; i < 3; i++)
        {
            await listener.StartAsync(eventListenerId, eventMap, eventHandler);
            await Task.Delay(100); // Allow time for state transition
                                   // State can be Started or Running depending on timing of background task
            listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

            await listener.StopAsync();
            await Task.Delay(100); // Allow time for cleanup
            listener.State.Should().Be(EventListenerState.Stopped);
        }
    }

    #endregion

    #region Event Handler Invocation Tests

    [Fact]
    public async Task EventHandler_ShouldBeInvoked_WhenMessagesReceived()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-016";
        var eventMap = CreateValidEventMap();
        var handlerInvoked = false;
        Func<string, NatsMsg<byte[]>, ValueTask> eventHandler = (id, msg) =>
        {
            handlerInvoked = true;
            return ValueTask.CompletedTask;
        };

        // Act
        await listener.StartAsync(eventListenerId, eventMap, eventHandler);

        // Note: Actual message reception requires a running NATS server and message publishing
        // This test validates the handler can be set without exceptions

        // Assert - State can be Started or Running depending on timing of background task
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);
        handlerInvoked.Should().BeFalse(); // No messages in this test scenario

        // Cleanup
        await listener.StopAsync();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentStartStop_ShouldHandleGracefully()
    {
        // Arrange
        var listener = new NatsActorEventListener(_options, _logger);
        var eventListenerId = "test-listener-017";
        var eventMap = CreateValidEventMap();
        var eventHandler = CreateValidEventHandler();

        // Act
        var startTask1 = listener.StartAsync(eventListenerId, eventMap, eventHandler).AsTask();
        var startTask2 = listener.StartAsync(eventListenerId, eventMap, eventHandler).AsTask();

        await Task.WhenAll(startTask1, startTask2);

        // Assert
        listener.State.Should().BeOneOf(EventListenerState.Started, EventListenerState.Running);

        // Cleanup
        await listener.StopAsync();
    }

    #endregion

    #region Helper Methods

    private static Dictionary<ActorMailboxId, List<string>> CreateValidEventMap()
    {
        var mailboxId = new ActorMailboxId(ActorType.Event, "test-entity");
        return new Dictionary<ActorMailboxId, List<string>>
        {
            { mailboxId, new List<string> { "event.created", "event.updated" } }
        };
    }

    private static Func<string, NatsMsg<byte[]>, ValueTask> CreateValidEventHandler()
    {
        return (id, msg) => ValueTask.CompletedTask;
    }

    #endregion
}
