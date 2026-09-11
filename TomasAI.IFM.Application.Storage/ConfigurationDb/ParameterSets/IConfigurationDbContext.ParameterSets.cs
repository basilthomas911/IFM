using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
public partial interface IConfigurationDbContext
{
 Task<ParameterLegacyVersion[]> ReadLegacyParameterVersionsAsync(Guid? setId=null,int version=0,int offset=0,CancellationToken token=default);
 Task<ParameterComponentSummary[]> ReadParameterComponentsAsync(CancellationToken token=default);
 Task<ParameterSchemaDefinition?> ReadParameterSchemaAsync(string componentCode,int version,CancellationToken token=default);
 Task ProjectParameterStartupAsync(ParameterStartupChangedEvent fact,CancellationToken token=default);
 Task ProjectParameterSetAsync(IParameterSetFact fact,CancellationToken token=default);
 Task<ParameterSetVersion[]> ReadParameterSetsAsync(string componentCode,Guid? setId=null,CancellationToken token=default,int limit=100,string afterName="",Guid? afterSetId=null,int afterVersion=0);
 Task ProjectParameterAssignmentAsync(ParameterAssignmentChangedEvent fact,CancellationToken token=default);
 Task<IAsyncDisposable> AcquireParameterWriteLeaseAsync(CancellationToken token=default);
}
