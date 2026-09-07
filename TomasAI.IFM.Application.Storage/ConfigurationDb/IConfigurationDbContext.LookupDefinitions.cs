using TomasAI.IFM.Domain.Reference.Shared.Lookups;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public partial interface IConfigurationDbContext
{
    Task<LookupDefinitionReadModel[]> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default);
}
