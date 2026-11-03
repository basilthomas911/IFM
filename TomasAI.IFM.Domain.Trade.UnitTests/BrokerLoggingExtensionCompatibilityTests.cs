using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public sealed class BrokerLoggingExtensionCompatibilityTests
{
    [Theory]
    [InlineData("Routed", 2510301, LogLevel.Information)]
    [InlineData("Failed", 2510399, LogLevel.Error)]
    public void Extension_wrapper_preserves_generated_event_identity_and_caller(string name, int id, LogLevel level)
    {
        var assembly = typeof(TomasAI.IFM.Domain.Trade.Order.Command.Actor.TradeOrderCommandActor).Assembly;
        var type = assembly.GetType("TomasAI.IFM.Domain.Trade.Order.Broker.Logging.BrokerOrderObservationLogging", throwOnError: true)!;
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotNull(method);
        var caller = method.GetParameters()[^1];
        Assert.NotNull(caller.GetCustomAttribute<CallerMemberNameAttribute>());
        Assert.Equal(string.Empty, caller.DefaultValue);
        var logger = new CaptureLogger();
        var observationId = Guid.NewGuid();
        var exception = new InvalidOperationException("expected failure");
        object?[] arguments = name == "Routed"
            ? [logger, "OrderStatus", "order-123", observationId, "OriginalCaller"]
            : [logger, exception, "Unavailable", "order-123", observationId, "OriginalCaller"];

        method.Invoke(null, arguments);

        Assert.Equal(id, logger.EventId.Id);
        Assert.Equal(name, logger.EventId.Name);
        Assert.Equal(level, logger.Level);
        Assert.Equal("OriginalCaller", logger.Fields["Method"]);
        Assert.Equal("order-123", logger.Fields["BrokerOrderId"]);
        Assert.Equal(observationId, logger.Fields["ObservationId"]);
        Assert.Same(name == "Failed" ? exception : null, logger.Exception);
    }

    private sealed class CaptureLogger : ILogger
    {
        public EventId EventId { get; private set; }
        public LogLevel Level { get; private set; }
        public Exception? Exception { get; private set; }
        public Dictionary<string, object?> Fields { get; private set; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            EventId = eventId;
            Level = logLevel;
            Exception = exception;
            Fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
