using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Model;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command;

/// <summary>Handles <see cref="CreateRegimeDiscoveryParameterSetCommand"/>.</summary>
public static class CreateRegimeDiscoveryParameterSet
{
    /// <summary>Applies the command's business guards and state transition.</summary>
    public static Task<ServiceResult<GuidResult>> ExecuteAsync(this CreateRegimeDiscoveryParameterSetCommand command, ICommandActorContext context, RegimeDiscoveryConfigurationCommandState state)
        => RegimeDiscoveryConfigurationTransition.ExecuteAsync(command, context, state);
}
