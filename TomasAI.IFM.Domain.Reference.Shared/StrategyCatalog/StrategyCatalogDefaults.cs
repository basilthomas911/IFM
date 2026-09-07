namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>The three initial strategy families and their supporting catalog definitions.</summary>
public static class StrategyCatalogDefaults
{
    static readonly (string Code, string Name, string[] Structures)[] Families =
    [
        ("DefaultFutures", "Futures", ["Future"]),
        ("DefaultVerticalSpreads", "Vertical Spreads", ["CallVertical", "PutVertical"]),
        ("DefaultIronCondor", "Iron Condor", ["IronCondor"])
    ];

    public static StrategyCatalogDefinition[] Create()
    {
        var examples = StrategyCatalogExamples.Create();
        var structures = examples.Where(x => x.Key.Kind == StrategyCatalogKind.Structure).ToArray();
        var families = Families.Select(x => StrategyCatalogExamples.New(StrategyCatalogKind.Family, x.Code + "Family", x.Name)
            with { Description = $"Default {x.Name} family. Configure its strategies, variants and deployments in the corresponding catalog sections." }).ToArray();
        var strategies = Families.Select((x, i) => StrategyCatalogExamples.New(StrategyCatalogKind.Strategy, x.Code, x.Name) with
        {
            Families = [families[i].Key], Structures = structures.Where(s => x.Structures.Contains(s.Code)).Select(s => s.Key).ToArray(),
            Description = $"Default {x.Name} strategy. Draft configuration; execution requires qualified capabilities.",
            Capabilities = [new("evaluator", "RegimeAligned", 1), new("data", "AcceptedMarketAssessment", 1)]
        });
        // Parents precede their dependent definitions for idempotent database initialization.
        return [.. families, .. structures, .. strategies, .. examples.Where(x => x.Key.Kind == StrategyCatalogKind.Variant)];
    }

    public static int DisplayOrder(StrategyCatalogSummary definition)
        => Array.FindIndex(Families, x => definition.Key.Id == StrategyCatalogExamples.StableId(
            definition.Key.Kind == StrategyCatalogKind.Family ? x.Code + "Family" : x.Code)) is var index && index >= 0 ? index : int.MaxValue;
}
