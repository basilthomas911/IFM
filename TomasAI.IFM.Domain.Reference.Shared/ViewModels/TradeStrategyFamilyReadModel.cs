using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public enum TradeStrategyFamilyState { Unknown = 0, Active = 1, Retired = 2 }

[MessagePackObject(AllowPrivate = true)]
public sealed record TradeStrategyFamilyReadModel
{
    [Key(0)] public int TradeStrategyFamilyId { get; init; }
    [Key(1)] public long DefinitionVersion { get; init; }
    [Key(2)] public string SystemKey { get; init; } = string.Empty;
    [Key(3)] public string Name { get; init; } = string.Empty;
    [Key(4)] public TradeStrategyFamilyState State { get; init; }
    [Key(5)] public DateTime CreatedOnUtc { get; init; }
    [Key(6)] public string CreatedBy { get; init; } = string.Empty;

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (TradeStrategyFamilyId <= 0 || DefinitionVersion <= 0) errors.Add("Positive family identity and definition version are required.");
        if (string.IsNullOrWhiteSpace(SystemKey) || string.IsNullOrWhiteSpace(Name)) errors.Add("SystemKey and Name are required.");
        if (State == TradeStrategyFamilyState.Unknown) errors.Add("State is required.");
        if (CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("UTC audit provenance is required.");
        return errors;
    }
}

public static class TradeStrategyFamilySeed
{
    public static readonly (string SystemKey, string Name)[] Definitions =
    [
        ("FUTURES", "Futures"),
        ("VERTICAL_SPREAD", "Vertical Spread"),
        ("IRON_CONDOR", "Iron Condor"),
    ];

    public static void Validate(IReadOnlyCollection<TradeStrategyFamilyReadModel> rows)
    {
        if (rows.Count != Definitions.Length) throw new InvalidOperationException("The v1 catalog must contain exactly three definitions.");
        var actual = rows.OrderBy(x => x.SystemKey).Select(x => (x.SystemKey, x.Name)).ToArray();
        var expected = Definitions.OrderBy(x => x.SystemKey).ToArray();
        if (!actual.SequenceEqual(expected) || rows.Any(x => x.DefinitionVersion != 1 || x.State != TradeStrategyFamilyState.Active))
            throw new InvalidOperationException("The v1 TradeStrategyFamily catalog is not canonical.");
    }
}
