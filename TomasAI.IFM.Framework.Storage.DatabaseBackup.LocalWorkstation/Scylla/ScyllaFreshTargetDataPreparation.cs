using System.Net;
using System.Text.Json;
using Cassandra;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

internal interface IScyllaFreshTargetDataPreparation
{
    void Validate(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target);
    ValueTask PrepareAsync(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target,
        string workspace, CancellationToken token);
}

/// <summary>Clears only an explicitly disposable isolated target before native schema restore.
/// Existing schema can block restoration and existing rows can overwrite restored rows. Source endpoints are excluded and the driver is
/// restricted to the configured target addresses; no application database is truncated.</summary>
internal sealed class ScyllaFreshTargetDataPreparation : IScyllaFreshTargetDataPreparation
{
    internal static IPAddress[] ValidateTarget(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target)
    {
        if (!target.DiscardExistingApplicationData || string.Equals(source.ManagerCluster, target.ManagerCluster, StringComparison.OrdinalIgnoreCase)
            || source.CqlContactPoints.Length == 0 || target.CqlContactPoints.Length == 0)
            throw new InvalidOperationException("Scylla restore requires explicit disposable-target preparation and distinct source/target CQL endpoints.");
        var protectedAddresses = source.CqlContactPoints.Select(value => IPAddress.Parse(value).MapToIPv6()).ToHashSet();
        var addresses = target.CqlContactPoints.Select(IPAddress.Parse).Distinct().ToArray();
        if (addresses.Any(address => protectedAddresses.Contains(address.MapToIPv6())))
            throw new InvalidOperationException("A fresh Scylla target overlaps the protected source endpoints.");
        return addresses;
    }

    public void Validate(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target)
        => _ = ValidateTarget(source, target);

    public async ValueTask PrepareAsync(ScyllaProtectionSetOptions source, ScyllaFreshTargetProfileOptions target,
        string workspace, CancellationToken token)
    {
        var addresses = ValidateTarget(source, target);
        using var cluster = Cluster.Builder().AddContactPoints(addresses.Select(address => address.ToString()).ToArray())
            .WithLoadBalancingPolicy(new IsolatedTargetPolicy(addresses))
            .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.All))
            .WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(10000).SetReadTimeoutMillis(120000))
            .Build();
        using var session = await cluster.ConnectAsync().WaitAsync(token).ConfigureAwait(false);
        if (cluster.AllHosts().Any(host => !addresses.Any(address => address.MapToIPv6().Equals(host.Address.Address.MapToIPv6()))))
            throw new InvalidOperationException("The disposable restore target discovered a node outside its approved endpoints.");
        var rows = await session.ExecuteAsync(new SimpleStatement("SELECT keyspace_name FROM system_schema.keyspaces;")).WaitAsync(token).ConfigureAwait(false);
        var keyspaces = rows.Select(row => row.GetValue<string>("keyspace_name"))
            .Where(name => !name.StartsWith("system", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        foreach (var keyspace in keyspaces)
            await session.ExecuteAsync(new SimpleStatement("DROP KEYSPACE " + Quote(keyspace) + ";")).WaitAsync(token).ConfigureAwait(false);
        var remaining = await session.ExecuteAsync(new SimpleStatement("SELECT keyspace_name FROM system_schema.keyspaces;")).WaitAsync(token).ConfigureAwait(false);
        if (remaining.Any(row => !row.GetValue<string>("keyspace_name").StartsWith("system", StringComparison.Ordinal)))
            throw new InvalidOperationException("The isolated Scylla restore target schema was not cleared.");
        await File.WriteAllTextAsync(Path.Combine(workspace, "fresh-target-preparation.json"), JsonSerializer.Serialize(new
        { target.ManagerCluster, TargetAddresses = addresses.Select(address => address.ToString()).ToArray(), ClearedKeyspaces = keyspaces,
            Preparation = "Discard isolated application schema before native schema restore", PreparedUtc = DateTimeOffset.UtcNow }), token).ConfigureAwait(false);
    }
    sealed class IsolatedTargetPolicy(IPAddress[] addresses) : ILoadBalancingPolicy
    {
        readonly RoundRobinPolicy _inner = new();
        bool Allowed(Host host) => addresses.Any(address => address.MapToIPv6().Equals(host.Address.Address.MapToIPv6()));
        public void Initialize(ICluster cluster) => _inner.Initialize(cluster);
        public HostDistance Distance(Host host) => Allowed(host) ? _inner.Distance(host) : HostDistance.Ignored;
        public IEnumerable<HostShard> NewQueryPlan(string keyspace, IStatement statement)
            => _inner.NewQueryPlan(keyspace, statement).Where(value => Allowed(value.Host));
    }
    static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
