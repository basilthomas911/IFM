namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class FeedRecoveryTests
{
    [Fact]
    public void Recover_UsesExactBackoffAndThirtySecondAttemptBudget()
    {
        var executor = new SequenceExecutor(3);
        var delay = new RecordingDelay();
        var subject = new DatabentoRecoveryOrchestrator(executor, delay);

        var result = subject.Recover(
            FeedRecoveryFaultKind.ConnectionHung,
            FeedRecoverySchema.Mbp1);

        Assert.True(result.IsReady(FeedRecoverySchema.Mbp1));
        Assert.Equal([1d, 2d, 5d], delay.Delays.Select(x => x.TotalSeconds));
        Assert.All(executor.Attempts, attempt =>
            Assert.Equal(TimeSpan.FromSeconds(30), attempt.Timeout));
        Assert.Equal(FeedReadinessState.Ready, subject.State);
        Assert.True(subject.EntryGateOpen);
    }

    [Theory]
    [InlineData(FeedRecoveryFaultKind.Authentication)]
    [InlineData(FeedRecoveryFaultKind.InvalidRequest)]
    [InlineData(FeedRecoveryFaultKind.SymbolResolution)]
    [InlineData(FeedRecoveryFaultKind.ProviderError)]
    public void Recover_DoesNotRetryPermanentFaults(FeedRecoveryFaultKind fault)
    {
        var executor = new SequenceExecutor(1);
        var delay = new RecordingDelay();
        var subject = new DatabentoRecoveryOrchestrator(executor, delay);

        var exception = Assert.Throws<DatabentoRecoveryException>(() =>
            subject.Recover(fault, FeedRecoverySchema.Trades));

        Assert.Equal(0, exception.Attempts);
        Assert.Empty(executor.Attempts);
        Assert.Empty(delay.Delays);
        Assert.False(subject.EntryGateOpen);
    }

    [Fact]
    public void TimestampCursor_DiscardsEarlierAndSavedDuplicateCount()
    {
        var cursor = new TimestampReplayCursor(100, 2);

        Assert.False(cursor.ShouldAccept(99));
        Assert.False(cursor.ShouldAccept(100));
        Assert.False(cursor.ShouldAccept(100));
        Assert.True(cursor.ShouldAccept(100));
        Assert.True(cursor.ShouldAccept(101));
    }

    [Fact]
    public void InitialReadiness_CannotOpenEntryGateWithIncompleteDefinitions()
    {
        var subject = new DatabentoRecoveryOrchestrator(
            new SequenceExecutor(1),
            new RecordingDelay());
        var incomplete = new FeedRecoveryResult
        {
            ConnectionAuthenticated = true,
            SubscriptionsAcknowledged = true,
            ReplayComplete = true,
            ContinuityVerified = true,
            RequiredBaselinesReady = true,
            DefinitionsComplete = false
        };

        Assert.Throws<InvalidOperationException>(() =>
            subject.EstablishInitialReadiness(incomplete, FeedRecoverySchema.Definitions));

        Assert.False(subject.EntryGateOpen);
    }

    [Fact]
    public void MboBaseline_RequiresSnapshotContinuityAndLiveBoundary()
    {
        var baseline = new MboRecoveryBaseline();
        baseline.BeginSnapshot();
        baseline.ApplySnapshotRecord(10);
        baseline.ApplySnapshotRecord(11);
        baseline.CompleteSnapshot();
        Assert.False(baseline.IsReady);

        baseline.ApplyLiveRecord(12);

        Assert.True(baseline.IsReady);
        Assert.Throws<InvalidDataException>(() => baseline.ApplyLiveRecord(14));
    }

    private sealed class RecordingDelay : IDatabentoRecoveryDelay
    {
        internal List<TimeSpan> Delays { get; } = [];
        public void Delay(TimeSpan duration) => Delays.Add(duration);
    }

    private sealed class SequenceExecutor(int readyOnAttempt)
        : IDatabentoRecoveryAttemptExecutor
    {
        internal List<FeedRecoveryAttempt> Attempts { get; } = [];

        public FeedRecoveryResult StopDisposeRecreateAndStart(FeedRecoveryAttempt attempt)
        {
            Attempts.Add(attempt);
            var ready = attempt.AttemptNumber >= readyOnAttempt;
            return new FeedRecoveryResult
            {
                ConnectionAuthenticated = ready,
                SubscriptionsAcknowledged = ready,
                ReplayComplete = ready,
                ContinuityVerified = ready,
                RequiredBaselinesReady = ready,
                DefinitionsComplete = ready,
                Failure = ready ? null : "not ready"
            };
        }
    }
}
