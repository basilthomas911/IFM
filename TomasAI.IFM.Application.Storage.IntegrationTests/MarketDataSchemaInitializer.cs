using System;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

internal static class MarketDataSchemaInitializer
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static void EnsureInitialized(IObjectRepository database)
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            AddColumn(database, "ALTER TABLE futures_rsi_signal ADD timePeriod text");
            AddColumn(database, "ALTER TABLE futures_rsi_signal ADD periodLength int");
            AddColumn(database, "ALTER TABLE futures_tdi_signal ADD timePeriod text");
            _initialized = true;
        }
    }

    private static void AddColumn(IObjectRepository database, string command)
    {
        try
        {
            database.Use(command).ExecuteCommandAsync().GetAwaiter().GetResult();
        }
        catch (StorageException exception) when (
            exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("conflicts with an existing column", StringComparison.OrdinalIgnoreCase))
        {
            // The local integration database has already been migrated.
        }
    }
}
