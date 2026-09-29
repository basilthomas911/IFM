using System;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

internal static class SequenceIdDatabaseInitializer
{
    public static void EnsureInitialized(SequenceIdSchemaDb db)
        => db.CreateAllAsync().GetAwaiter().GetResult();
}
