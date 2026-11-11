using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;

/// <summary>Qualifies accepted parameter identities before the projection adapter is invoked.</summary>
internal static class AcceptedParameterIdentity
{
    internal static void RequireSet(IParameterSetFact fact)
        => RequireValue(fact.ReadVersion().Values());

    internal static void RequireStartup(ParameterStartupChangedEvent fact)
    {
        foreach (var version in fact.ReadRun()?.Versions ?? [])
            RequireValue(version.Values());
    }

    static void RequireValue(ParameterSetValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SetId == Guid.Empty || value.Version < 1)
            throw new ArgumentException("Parameter identity and version are required", nameof(value));
    }
}
