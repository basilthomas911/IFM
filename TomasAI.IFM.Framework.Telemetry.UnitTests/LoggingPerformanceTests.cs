using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Shared.Telemetry;
using Xunit;

namespace TomasAI.IFM.Framework.Telemetry.UnitTests;

public sealed class LoggingPerformanceTests
{
    [Fact]
    public void DisabledCompiledLoggingDoesNotAllocate()
    {
        for (var i = 0; i < 1000; i++) Log();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100000; i++) Log();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        static void Log() => StructuredOperationLogging.Completed(NullLogger.Instance, "Worker", "ExecuteAsync", "Contract=ES", "Completed", 1.2);
    }

    [Fact]
    public void RepeatedWarningsReportSuppressionAtNextWindow()
    {
        var clock = new ManualClock();
        var gate = new RepeatedLogGate<string>(TimeSpan.FromSeconds(5), clock: clock);
        Assert.True(gate.ShouldLog("ES", out _));
        for (var i = 0; i < 100; i++) Assert.False(gate.ShouldLog("ES", out _));
        clock.Timestamp = 5000;
        Assert.True(gate.ShouldLog("ES", out var suppressed));
        Assert.Equal(100, suppressed);
    }

    [Fact]
    public void HashCollisionDoesNotHideAnotherSubjectsFirstWarning()
    {
        var gate = new RepeatedLogGate<string>(TimeSpan.FromSeconds(5), capacity: 1);
        Assert.True(gate.ShouldLog("ES", out _));
        Assert.True(gate.ShouldLog("VX", out _));
    }

    sealed class ManualClock : TimeProvider
    {
        public long Timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Timestamp;
    }
}
