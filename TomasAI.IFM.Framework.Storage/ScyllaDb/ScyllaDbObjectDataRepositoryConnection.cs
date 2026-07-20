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
    Cluster _cluster;
    CassandraConnectionStringBuilder? _stringBuilder;
    ISession? _session;
    readonly SemaphoreSlim _sessionLock = new(1, 1);

    public ScyllaDbConnection(string connectionString)
    {
        _cluster =  ConnectToCluster(connectionString);
    }

    public string ClusterName => _stringBuilder!.ClusterName;
    public string DefaultKeyspace => _stringBuilder!.DefaultKeyspace;    
    public int Port => _stringBuilder!.Port;
    public string[] ContactPoints => _stringBuilder!.ContactPoints;
    public string Username => _stringBuilder!.Username;
    public string Password => _stringBuilder!.Password;

    /// <summary>
    /// Returns the cached session or creates one on first call.
    /// The Cassandra ISession is thread-safe and designed for reuse.
    /// </summary>
    public async Task<ISession> CreateSessionAsync()
    {
        if (_session is not null) return _session;
        await _sessionLock.WaitAsync();
        try
        {
            return _session ??= await _cluster.ConnectAsync(DefaultKeyspace);
        }
        finally
        {
            _sessionLock.Release();
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
            _stringBuilder = new CassandraConnectionStringBuilder(connectionString);
            return Cluster.Builder()
                .AddContactPoints(ContactPoints)
                .WithPort(Port)
                .WithCredentials(Username, Password)
                .WithTypeSerializers(definitions)
                .WithQueryTimeout(30000)
                .WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(30000))
                .WithPoolingOptions(poolingOptions)
                .Build();
        }
        catch (Exception ex)
        {
            throw new StorageException($"{ClassName}.ConnectToCluster: Failed to connect to ScyllaDb cluster", ex);
        }
    }
}
