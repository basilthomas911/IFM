using System;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

internal static class SequenceIdDatabaseInitializer
{
    static readonly object Sync = new();
    static bool _initialized;

    public static void EnsureInitialized(SequenceIdDbContext db)
    {
        lock (Sync)
        {
            if (_initialized)
                return;

            db.Use(SequenceIdDbSql.CreateGetCurrentSequenceIdFunction)
                .ExecuteCommandAsync().GetAwaiter().GetResult();
            db.Use(SequenceIdDbSql.CreateGetNextSequenceIdFunction)
                .ExecuteCommandAsync().GetAwaiter().GetResult();

            foreach (var sequenceName in Enum.GetNames<SequenceName>())
            {
                db.Use($"""
                    CREATE SEQUENCE IF NOT EXISTS {sequenceName}
                    START WITH 1
                    INCREMENT BY 100
                    NO MINVALUE
                    NO MAXVALUE
                    CACHE 1;
                    """).ExecuteCommandAsync().GetAwaiter().GetResult();
            }

            _initialized = true;
        }
    }
}
