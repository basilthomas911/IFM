using System.Data;
using Cassandra;
using Cassandra.Serialization;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public class ScyllaDbObjectDataRepositoryConnection : IObjectRepositoryConnection<ScyllaDbConnection>
{
    /// <summary>
    /// Create a ScyllaDb connection
    /// </summary>
    /// <typeparam name="TConnection"></typeparam>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection
        => throw new NotImplementedException($"{nameof(ScyllaDbObjectDataRepositoryConnection)}.As<TConnection>: create ScyllaDbConnection directly in provider code");
}

internal class  ScyllaDbConnection  
{
    const string ClassName = nameof(ScyllaDbConnection);
    readonly Cluster _cluster;
    CassandraConnectionStringBuilder? _stringBuilder;
    Lazy<Task<ISession>>? _sessionFactory;

    public ScyllaDbConnection(string connectionString)
    {
        _cluster =  ConnectToCluster(connectionString);
    }

    public string ClusterName => _stringBuilder!.ClusterName;
    public string DefaultKeyspace => _stringBuilder!.DefaultKeyspace;    
    public int Port => _stringBuilder!.Port;
    public string[] ContactPoints => _stringBuilder!.ContactPoints;

    internal static QueryOptions CreateQueryOptions()
        => new QueryOptions()
            .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
            .SetSerialConsistencyLevel(ConsistencyLevel.LocalSerial);
    /// <summary>
    /// Returns the cached session or creates one on first call.
    /// The Cassandra ISession is thread-safe and designed for reuse.
    /// </summary>
    public async Task<ISession> CreateSessionAsync()
    {
        var factory = Volatile.Read(ref _sessionFactory);
        if (factory is null)
        {
            var candidate = new Lazy<Task<ISession>>(
                () => _cluster.ConnectAsync(DefaultKeyspace),
                LazyThreadSafetyMode.ExecutionAndPublication);
            factory = Interlocked.CompareExchange(ref _sessionFactory, candidate, null) ?? candidate;
        }

        try
        {
            return await factory.Value.ConfigureAwait(false);
        }
        catch
        {
            Interlocked.CompareExchange(ref _sessionFactory, null, factory);
            throw;
        }
    }

    /// <summary>
    /// Connect to ScyllaDb cluster
    /// </summary>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    /// <exception cref="StorageException"></exception>
    Cluster ConnectToCluster(string connectionString)
    {
        try
        {
            // Configure pooling options
            var poolingOptions = new PoolingOptions()
                // Set the maximum number of connections per host to 32
                .SetMaxConnectionsPerHost(HostDistance.Local, 32)
                // Set the core number of connections per host
                .SetCoreConnectionsPerHost(HostDistance.Local, 2)
                // Optional: Set thresholds for opening new connections
                .SetMaxSimultaneousRequestsPerConnectionTreshold(HostDistance.Local, 2048);

            TypeSerializerDefinitions definitions = new TypeSerializerDefinitions();
            //definitions.Define(new DateTimeToLocalDateTypeSerializer());
            definitions.Define(new DateOnlyToLocalDateTypeSerializer());
            definitions.Define(new TimeOnlyToLocalTimeTypeSerializer());
            _stringBuilder = DatabaseCredentialResolver.GetScyllaConnectionSettings(connectionString);
            var credentials = DatabaseCredentialResolver.Resolve(DatabaseProvider.ScyllaDb);
            return Cluster.Builder()
                .AddContactPoints(ContactPoints)
                .WithPort(Port)
                .WithCredentials(credentials.UserId, credentials.Password)
                .WithTypeSerializers(definitions)
                .WithQueryTimeout(30000)
                .WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(30000))
                .WithPoolingOptions(poolingOptions)
                .WithQueryOptions(CreateQueryOptions())
                .Build();
        }
        catch (Exception ex)
        {
            throw new StorageException($"{ClassName}.ConnectToCluster: Failed to connect to ScyllaDb cluster", ex);
        }
    }
}
