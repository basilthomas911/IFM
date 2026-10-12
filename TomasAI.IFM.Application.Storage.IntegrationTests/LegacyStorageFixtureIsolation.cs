using System;
using System.Text.RegularExpressions;
using Cassandra;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

/// <summary>Prepares only explicitly named local legacy test databases and keyspaces.</summary>
internal static class LegacyStorageFixtureIsolation
{
    static readonly System.Threading.Lock Gate = new();
    static readonly Regex Name = new("^ifm_storage_legacy_[a-z0-9_]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string Scylla(string keyspace)
    {
        Validate(keyspace);
        lock (Gate)
        {
            using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(9042).Build();
            using var session = cluster.Connect();
            session.Execute("CREATE KEYSPACE IF NOT EXISTS " + keyspace +
                " WITH replication = {'class':'SimpleStrategy','replication_factor':1};");
        }
        return "Contact Points=localhost;Port=9042;Default Keyspace=" + keyspace;
    }

    public static string Postgres(string database)
    {
        Validate(database);
        lock (Gate)
        {
            using var connection = new PostgresObjectDataRepositoryConnection()
                .As<NpgsqlConnection>("Host=localhost;Port=5432;Database=postgres");
            connection.Open();
            using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=$1", connection);
            exists.Parameters.AddWithValue(database);
            if (Convert.ToInt64(exists.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0)
            {
                using var create = new NpgsqlCommand("CREATE DATABASE " + database, connection);
                create.ExecuteNonQuery();
            }
        }
        return "Host=localhost;Port=5432;Database=" + database;
    }

    static void Validate(string name)
    {
        if (!Name.IsMatch(name)) throw new ArgumentException("Only dedicated legacy test identifiers are accepted.", nameof(name));
    }
}
