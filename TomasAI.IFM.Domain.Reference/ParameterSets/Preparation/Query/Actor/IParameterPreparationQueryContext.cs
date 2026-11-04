using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;

/// <summary>Read-only owner services used to prepare explicit command evidence.</summary>
public interface IParameterPreparationQueryContext : IQueryActorContext<ParameterPreparationQueryActor>
{
    IParameterAccessPolicy AccessPolicy { get; }
    IConfigurationDbContext ConfigurationDb { get; }
    ILogger<ParameterPreparationQueryActor> Logger { get; }
}
