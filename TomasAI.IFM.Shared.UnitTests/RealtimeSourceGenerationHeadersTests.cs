using System;
using Xunit;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.UnitTests;

public sealed class RealtimeSourceGenerationHeadersTests
{
    [Fact]
    public void Tagged_event_round_trips_generation_without_business_payload_changes()
    {
        var generation = Guid.NewGuid();
        var headers = RealtimeSourceGenerationHeaders.Add(null,
            new Tagged("GLBX.MDP3", generation));

        Assert.True(RealtimeSourceGenerationHeaders.TryRead(headers, out var dataset,
            out var observed));
        Assert.Equal("GLBX.MDP3", dataset);
        Assert.Equal(generation, observed);
    }

    [Fact]
    public void Untagged_event_has_no_generation_headers()
    {
        Assert.Null(RealtimeSourceGenerationHeaders.Add(null, new object()));
        Assert.False(RealtimeSourceGenerationHeaders.TryRead(null, out _, out _));
    }

    private sealed record Tagged(string SourceDataset, Guid SourceGenerationId)
        : IRealtimeSourceGeneration;
}
