using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.ParameterSets;

/// <summary>Ensures the initial Futures ITI parameter set exists through the event-sourced command path.</summary>
internal static class FuturesItiSignalDefaultParameterSet
{
    internal const string Name = "Future ITI Signal";
    internal static readonly Guid SetId = StableId("parameter-set:market-data-analytics:futures-iti-signal:default");
    static readonly Guid CommandId = StableId("parameter-command:create:market-data-analytics:futures-iti-signal:default:v1");

    /// <summary>Creates the immutable version-one draft when the set has not already been created.</summary>
    internal static async ValueTask EnsureAsync(IParameterSetsApi parameterSets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameterSets);

        var existing = await parameterSets.StateAsync(SetId, cancellationToken).ConfigureAwait(false);
        if (!existing.Success || existing.Value is null)
            throw new InvalidOperationException(existing.ErrorMessage ?? "The Futures ITI parameter-set state is unavailable.");
        if (existing.Value.Versions.Length != 0)
        {
            Validate(existing.Value.Versions.OrderByDescending(version => version.Reference.Version).First(), false);
            return;
        }

        var payload = JsonSerializer.Serialize(new FuturesItiSignalParameterSet
        {
            ParameterSetId = SetId,
            DefaultTradingDays = new()
            {
                Daily = 1,
                Weekly = 10,
                Monthly = 30
            }
        });
        var created = await parameterSets.CreateAsync(new CreateParameterSetCommand
        {
            CommandId = CommandId,
            EntityId = new(SetId),
            ComponentCode = ParameterSchemaRegistry.FuturesItiSignalComponent,
            Name = Name,
            Description = "Default trading-day values for Daily, Weekly, and Monthly Futures ITI signals.",
            SchemaVersion = ParameterSchemaRegistry.CurrentFuturesItiSignalSchemaVersion,
            PayloadJson = payload
        }, cancellationToken).ConfigureAwait(false);
        if (!created.Success)
            throw new InvalidOperationException(created.ErrorMessage ?? "The Futures ITI parameter set could not be created.");

        var saved = await parameterSets.StateAsync(SetId, cancellationToken).ConfigureAwait(false);
        var version = saved.Success
            ? saved.Value?.Versions.SingleOrDefault(candidate => candidate.Reference.Version == 1)
            : null;
        if (version is null)
            throw new InvalidOperationException("The created Futures ITI parameter-set version is unavailable.");
        Validate(version, true);
    }

    static void Validate(ParameterSetVersion version, bool requireInitialDefaults)
    {
        if (version.Reference.ComponentCode != ParameterSchemaRegistry.FuturesItiSignalComponent
            || version.SchemaVersion != ParameterSchemaRegistry.CurrentFuturesItiSignalSchemaVersion)
            throw new InvalidOperationException("The stored Futures ITI parameter-set identity or schema is invalid.");

        var payload = JsonSerializer.Deserialize<FuturesItiSignalParameterSet>(version.PayloadJson)
            ?? throw new InvalidOperationException("The stored Futures ITI parameter-set payload is invalid.");
        if (payload.ParameterSetId != SetId
            || payload.DefaultTradingDays.Daily <= 0
            || payload.DefaultTradingDays.Weekly <= 0
            || payload.DefaultTradingDays.Monthly <= 0)
            throw new InvalidOperationException("The stored Futures ITI trading-day values are invalid.");
        if (requireInitialDefaults
            && payload.DefaultTradingDays is not { Daily: 1, Weekly: 10, Monthly: 30 })
            throw new InvalidOperationException("The initial Futures ITI default trading-day values do not match the required baseline.");
    }

    static Guid StableId(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
