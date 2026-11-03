using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Xunit;

namespace TomasAI.IFM.Application.Actor.IntegrationTests.Architecture;

public sealed class BrokerAccountLoggingExtensionTests
{
    [Fact]
    public void Account_failure_extension_preserves_structured_event_and_original_caller()
    {
        var assembly = Assembly.Load("TomasAI.IFM.Domain.BrokerAccount");
        var type = assembly.GetType("TomasAI.IFM.Domain.BrokerAccount.Logging.BrokerAccountLogging", throwOnError: true)!;
        var method = type.GetMethod("AccountObservationFailed", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.NotNull(method.GetParameters()[^1].GetCustomAttribute<CallerMemberNameAttribute>());
        var logger = new CaptureLogger();
        var exception = new InvalidOperationException("expected failure");
        var observationId = Guid.NewGuid();

        method.Invoke(null, [logger, exception, "test-account", observationId, "ObservationCaller"]);

        Assert.Equal(new EventId(25601, "AccountObservationFailed"), logger.EventId);
        Assert.Equal("AccountObservationFailed", logger.EventId.Name);
        Assert.Same(exception, logger.Exception);
        Assert.Equal("test-account", logger.Fields["AccountAlias"]);
        Assert.Equal(observationId, logger.Fields["ObservationId"]);
        Assert.Equal("ObservationCaller", logger.Fields["Method"]);
    }

    private sealed class CaptureLogger : ILogger
    {
        public EventId EventId { get; private set; }
        public Exception? Exception { get; private set; }
        public Dictionary<string, object?> Fields { get; private set; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Equal(LogLevel.Error, logLevel);
            EventId = eventId;
            Exception = exception;
            Fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
