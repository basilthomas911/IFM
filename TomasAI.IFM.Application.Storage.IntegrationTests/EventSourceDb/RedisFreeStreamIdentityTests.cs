using Npgsql;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class RedisFreeStreamIdentityTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task GivenConcurrentColdOwnersWhenResolvingAStreamThenPostgresOwnsOneIdentity()
    {
        var stream = "RedisRemoval.Concurrent." + Guid.NewGuid().ToString("N");
        await using var first = fixture.CreateActorEventDb();
        await using var second = fixture.CreateActorEventDb();
        var requests = Enumerable.Range(0, 32)
            .Select(index => (index % 2 == 0 ? first : second).GetEventStreamIdAsync(stream)).ToArray();
        var identities = await Task.WhenAll(requests);
        Assert.All(identities, id => Assert.Equal(identities[0], id));
        Assert.True(identities[0] > 0);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM event_stream_id WHERE eventstream=$1", connection);
        count.Parameters.AddWithValue(stream);
        Assert.Equal(1L, await count.ExecuteScalarAsync());
        first._streamIdentities.Clear();
        Assert.Equal(identities[0], await first.GetEventStreamIdAsync(stream));
        Assert.Equal(1, first._streamIdentities.Count);
    }

    [Fact]
    public async Task GivenReplacedMetadataWhenOwnersRestartThenNoRedisIdentitySurvives()
    {
        var stream = "RedisRemoval.Restore." + Guid.NewGuid().ToString("N");
        var oldOwner = fixture.CreateActorEventDb();
        var previousId = await oldOwner.GetEventStreamIdAsync(stream);
        await oldOwner.DisposeAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        // This fixture stream contains no events. Replacing its metadata simulates the identity boundary
        // of a restored database after every cache owner has stopped, without modifying Development.
        await using var replace = new NpgsqlCommand("DELETE FROM event_stream_id WHERE eventstream=$1", connection);
        replace.Parameters.AddWithValue(stream);
        Assert.Equal(1, await replace.ExecuteNonQueryAsync());
        await using var restarted = fixture.CreateActorEventDb();
        var currentId = await restarted.GetEventStreamIdAsync(stream);
        Assert.NotEqual(previousId, currentId);
        Assert.Equal(currentId, await restarted.GetEventStreamIdAsync(stream));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => oldOwner.GetEventStreamIdAsync(stream));
    }
}
