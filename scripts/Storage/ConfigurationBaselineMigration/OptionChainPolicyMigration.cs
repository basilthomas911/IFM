using Cassandra;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
namespace TomasAI.IFM.Storage.Tools;

/// <summary>Copies the exact small Development policy tables without changing their source or publishing new policies.</summary>
internal static class OptionChainPolicyMigration
{
    internal static async Task<object> RunAsync(IPostgresEventTransaction transactions)
    {
        var source=Environment.GetEnvironmentVariable("IFM_OPTION_CHAIN_POLICY_SOURCE")
            ?? throw new InvalidOperationException("Set IFM_OPTION_CHAIN_POLICY_SOURCE to the local Development Scylla connection.");
        var settings=new CassandraConnectionStringBuilder(source);
        if (settings.ContactPoints.Any(x=>x is not ("localhost" or "127.0.0.1")) ||
            settings.DefaultKeyspace is not "market_data_test_db")
            throw new InvalidOperationException("Policy migration is limited to local market_data_test_db.");
        using var cluster=Cluster.Builder().AddContactPoints(settings.ContactPoints).WithPort(settings.Port).Build();
        using var session=await cluster.ConnectAsync(settings.DefaultKeyspace).ConfigureAwait(false);
        var versions=(await session.ExecuteAsync(new SimpleStatement("SELECT set_id,version,payload FROM strategy_option_chain_parameter_version;")
            .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)).ConfigureAwait(false))
            .Select(row=>new Version(row.GetValue<Guid>("set_id"),row.GetValue<int>("version"),row.GetValue<string>("payload"))).ToArray();
        var current=(await session.ExecuteAsync(new SimpleStatement("SELECT environment,set_id,version,revision,published,payload FROM strategy_option_chain_parameter_current;")
            .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)).ConfigureAwait(false))
            .Select(row=>new Current(row.GetValue<string>("environment"),row.GetValue<Guid>("set_id"),row.GetValue<int>("version"),
                row.GetValue<long>("revision"),row.GetValue<bool>("published"),row.GetValue<string>("payload"))).ToArray();
        foreach(var row in current)
            if(row.Revision<1 || !versions.Any(x=>x.Id==row.Id && x.Number==row.Number && string.Equals(x.Payload,row.Payload,StringComparison.Ordinal)))
                throw new InvalidDataException("Source policy revision/content does not match its immutable version.");
        await transactions.ExecuteOnceAsync(async(db,ct)=>{
            await db.ExecuteAsync(MarketDataSchemaSql.OptionChainPoliciesCreate,[],ct).ConfigureAwait(false);
            foreach(var row in versions)await CopyVersionAsync(db,row,ct).ConfigureAwait(false);
            foreach(var row in current)await CopyCurrentAsync(db,row,ct).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
        return new {sourceKeyspace=settings.DefaultKeyspace,versions=versions.Length,current=current.Length,
            copiedWithoutSourceMutation=true,verifiedPayloads=versions.Select(x=>new {setId=x.Id,version=x.Number,sha256=Hash(x.Payload)}).ToArray()};
    }

    static async Task CopyVersionAsync(EnlistedEventTransaction db,Version row,CancellationToken ct)
    {
        await db.ExecuteAsync("INSERT INTO market_data.strategy_option_chain_parameter_version(set_id,version,payload) VALUES($1,$2,$3) ON CONFLICT DO NOTHING;",[row.Id,row.Number,row.Payload],ct).ConfigureAwait(false);
        var value=await db.ScalarAsync("SELECT payload FROM market_data.strategy_option_chain_parameter_version WHERE set_id=$1 AND version=$2;",[row.Id,row.Number],ct).ConfigureAwait(false);
        if(value is not string payload || !string.Equals(payload,row.Payload,StringComparison.Ordinal))
            throw new InvalidDataException("Target policy version differs; the entire migration transaction is rolled back.");
    }

    static async Task CopyCurrentAsync(EnlistedEventTransaction db,Current row,CancellationToken ct)
    {
        await db.ExecuteAsync("INSERT INTO market_data.strategy_option_chain_parameter_current(environment,set_id,version,revision,published,payload) VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING;",[row.Environment,row.Id,row.Number,row.Revision,row.Published,row.Payload],ct).ConfigureAwait(false);
        var values=await db.QueryAsync("SELECT revision,published,payload FROM market_data.strategy_option_chain_parameter_current WHERE environment=$1 AND set_id=$2 AND version=$3;",[row.Environment,row.Id,row.Number],
            reader=>(Revision:reader.GetInt64(0),Published:reader.GetBoolean(1),Payload:reader.GetString(2)),ct).ConfigureAwait(false);
        var value=values.Single();
        if(value.Revision!=row.Revision || value.Published!=row.Published || !string.Equals(value.Payload,row.Payload,StringComparison.Ordinal))
            throw new InvalidDataException("Target current policy differs; the entire migration transaction is rolled back.");
    }

    static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    sealed record Version(Guid Id,int Number,string Payload);
    sealed record Current(string Environment,Guid Id,int Number,long Revision,bool Published,string Payload);
}
