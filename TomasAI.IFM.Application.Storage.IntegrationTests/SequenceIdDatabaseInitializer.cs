using System;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

internal static class SequenceIdDatabaseInitializer
{
    static readonly object Sync = new();
    static bool _initialized;

    public static void EnsureInitialized(SequenceIdSchemaDb db)
    {
        lock (Sync)
        {
            if (_initialized)
                return;

            db.CreateAllAsync().GetAwaiter().GetResult();

            _initialized = true;
        }
    }
}
