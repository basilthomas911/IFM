namespace TomasAI.IFM.Application.Storage.SchemaDb;

public sealed record SchemaObjectDefinition(
    string Name,
    string CreateStatement,
    string DropStatement,
    IReadOnlyCollection<string>? AlreadyAppliedErrorFragments = null);
