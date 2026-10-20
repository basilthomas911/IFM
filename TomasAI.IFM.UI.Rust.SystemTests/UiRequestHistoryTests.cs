using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class UiRequestHistoryTests
{
    [Fact]
    public void Completion_immediately_removes_pending_work_and_oldest_history_is_evicted_first()
    {
        var history = new UiRequestHistory<int>(capacity: 4);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        for (var i = 0; i < 4; i++)
        {
            Assert.True(history.TryBegin(ids[i], i, "Pending"));
            Assert.Equal(1, history.PendingCount);
            history.Complete(ids[i], new(ids[i], "Completed", "Done"));
            Assert.Equal(0, history.PendingCount);
        }
        Assert.True(history.TryBegin(ids[4], 4, "Pending"));
        Assert.False(history.TryGetValue(ids[0], out _));
        Assert.True(history.TryGetValue(ids[1], out _));
        Assert.Equal(3, history.CompletedCount);
        Assert.Equal(1, history.PendingCount);
        Assert.Equal("Pending", history.Outcomes().Last().State);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Unknown")]
    public void Every_terminal_outcome_leaves_the_pending_queue(string state)
    {
        var history = new UiRequestHistory<int>(capacity: 4);
        for (var i = 0; i < 1_000; i++)
        {
            var id = Guid.NewGuid();
            Assert.True(history.TryBegin(id, i, "Pending"));
            history.Complete(id, new(id, state, "Finished"));
            Assert.Equal(0, history.PendingCount);
            Assert.InRange(history.CompletedCount, 1, 4);
        }
    }

    [Fact]
    public void Active_requests_are_not_evicted_when_completed_history_expires()
    {
        var history = new UiRequestHistory<int>(capacity: 4, shortHistoryLimit: 2);
        var active = Guid.NewGuid();
        Assert.True(history.TryBegin(active, -1, "Pending"));
        for (var i = 0; i < 10_000; i++)
        {
            var id = Guid.NewGuid();
            Assert.True(history.TryBegin(id, i, "Pending"));
            history.Complete(id, new(id, "Completed", "Done"), shortLived: true);
        }
        Assert.True(history.TryGetValue(active, out var pending));
        Assert.Equal("Pending", pending.Outcome.State);
        Assert.Equal(1, history.PendingCount);
        Assert.Equal(2, history.CompletedCount);
        Assert.Equal(3, history.Outcomes().Length);
    }

    [Fact]
    public async Task Trade_load_and_paging_continue_beyond_the_lifetime_request_limit()
    {
        var calls = 0;
        await using var gateway = new ContractEditorGateway(input => {
            calls++;
            return Task.FromResult(new ContractEditorResult(input.View, [], []));
        }, _ => true);
        Assert.Equal(0, gateway.Submit(new(1, Guid.NewGuid(), "trade-orders", "load", 0, null, [])));
        await gateway.DrainAsync();
        calls = 0;
        ContractEditorIntent? latest = null;
        for (var i = 0; i < 2_000; i++)
        {
            latest = new(1, Guid.NewGuid(), "trade-orders", i % 2 == 0 ? "load-trade" : "plans-page",
                gateway.Snapshot().Generation, null, []);
            Assert.Equal(0, gateway.Submit(latest));
            await gateway.DrainAsync();
            Assert.False(gateway.Snapshot().Busy);
            Assert.InRange(gateway.Snapshot().Outcomes.Length, 1, 256);
            Assert.Equal(latest.RequestId, gateway.Snapshot().Outcomes.Last().RequestId);
        }
        Assert.Equal(2_000, calls);
        Assert.Equal(0, gateway.Submit(latest!));
        Assert.Equal(2_000, calls);
        Assert.Equal(-1, gateway.Submit(latest! with { Intent = "change" }));
    }

    [Fact]
    public async Task Completed_lookup_edits_do_not_exhaust_the_gateway()
    {
        await using var gateway = new EditorActionGateway(_ => Task.FromResult<TomasAI.IFM.UI.Net.Models.Reference.LookupTypeUiModel[]>([]));
        var row = new TomasAI.IFM.UI.Net.Models.Reference.LookupTypeUiModel("Test", "A", 1, "Description", DateTime.UtcNow, "test");
        for (var i = 0; i < 1_000; i++)
        {
            Assert.Equal(0, gateway.Submit(new(1, Guid.NewGuid(), "lookup", "add", gateway.Snapshot().Generation, row)));
            await gateway.DrainAsync();
        }
        Assert.Equal(256, gateway.Snapshot().Outcomes.Length);
        Assert.All(gateway.Snapshot().Outcomes, outcome => Assert.Equal("Completed", outcome.State));
    }
}
