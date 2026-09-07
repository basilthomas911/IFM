using TomasAI.IFM.Domain.Reference.Shared.Lookups;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public sealed partial class ConfigurationDbContext
{
    public async Task<LookupDefinitionReadModel[]> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName) || groupName.Length > 64 || !char.IsAsciiLetter(groupName[0]) || groupName.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("A valid lookup group is required.", nameof(groupName));
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
SELECT id,group_name,internal_value,display_name,description,display_order,is_enabled,created_utc,updated_utc
FROM reference_configuration.lookup_definition WHERE group_name=$1 ORDER BY display_order,id LIMIT 1025;
""", groupName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<LookupDefinitionReadModel> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),
                reader.GetInt32(5),reader.GetBoolean(6),reader.GetDateTime(7),reader.GetDateTime(8)));
        if (rows.Count > 1024) throw new InvalidOperationException("Lookup group exceeds the supported response size.");
        return rows.ToArray();
    }
}
