using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// One isolated lookup partition used by G2-024 through G2-026.
/// </summary>
public sealed record G2LookupTypeFixture(
    LookupTypeReadModel AddedLookupType,
    LookupTypeReadModel ChangedLookupType,
    string DefinitionDescription)
{
    public static async Task<G2LookupTypeFixture> CreateAsync(
        G0QuerySession queries,
        G2Configuration configuration,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(configuration);

        var definitions = Require(
                await queries.Reference.GetReferenceDataDefinitionTypesAsync()
                    .WaitAsync(timeout, cancellationToken),
                "ReferenceDataDefinitionType lookup")
            .ToArray();
        var description = definitions.SingleOrDefault(value => string.Equals(
                value.ShortCode,
                "LookupTypes",
                StringComparison.OrdinalIgnoreCase))?.Description
            ?? throw new G0DependencyException(
                "The ReferenceDataDefinitionType lookup does not contain required short code 'LookupTypes'.");
        var lookupTypeName = $"{configuration.RunPrefix}-Lookup";
        var createdOn = DateTime.UtcNow;
        var added = new LookupTypeReadModel(
            lookupTypeName,
            $"{configuration.RunPrefix}-A",
            0,
            $"{configuration.RunPrefix} lookup added",
            createdOn,
            "G2 UI system test");
        var changed = added with
        {
            ShortCode = $"{configuration.RunPrefix}-B",
            Description = $"{configuration.RunPrefix} lookup changed"
        };
        return new G2LookupTypeFixture(added, changed, description);
    }

    static T Require<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new G0DependencyException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }
}
