namespace TomasAI.IFM.Application.Storage.Schema;

public sealed record SchemaObjectDefinition(
    string Name,
    string CreateStatement,
    string DropStatement,
    IReadOnlyCollection<string>? AlreadyAppliedErrorFragments = null);
