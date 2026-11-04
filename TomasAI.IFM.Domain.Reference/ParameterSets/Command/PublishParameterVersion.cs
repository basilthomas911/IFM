using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command;

public static class PublishParameterVersion
{

    /// <param name="command">
    /// The concrete lifecycle intent.
    /// The originating command.
    /// </param>
    extension(PublishParameterVersionCommand command)
    {
        /// <summary>Authorizes the request, checks replay, and applies its computed source event.</summary>
        ///
        /// <param name="context">The authorized command services and repositories.</param>
        /// <param name="state">The authoritative actor state.</param>
        /// <param name="logger">The actor logger.</param>
        /// <returns>The command acceptance or business failure.</returns>
        public Task<ServiceResult<GuidResult>> ExecuteAsync(IParameterSetCommandContext context, ParameterSetCommandState state, ILogger<ParameterSetCommandActor> logger)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Publish);
            var hash = ParameterMutationModel.RequestHash(command);
            if (state.Operations.TryGetValue(command.CommandId, out var previous))
            {
                if (!string.Equals(previous, hash, StringComparison.Ordinal)) throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
                return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
            }
            var errorMsg = "ParameterSet.STATE.APPLY_FAILED: unable to apply PublishParameterVersion event";
            command.Compute(state, hash, out var parameterChange);
            if (parameterChange.RejectionReason is null && parameterChange.ParameterVersion is { } version
                && string.Equals(version.Reference.ComponentCode, ParameterSchemaRegistry.StrategyOptionChainCacheComponent, StringComparison.Ordinal))
            {
                var policy = version.Values().Get<StrategyOptionChainParameterSet>();
                if (policy.Enabled)
                {
                    var catalog = command.Evidence?.Catalog;
                    if (command.Evidence?.ExactVersion?.Reference != version.Reference || catalog is null
                        || catalog.DefinitionId != policy.StrategyDefinitionId || catalog.Version != policy.StrategyDefinitionVersion
                        || string.IsNullOrEmpty(catalog.ContentHash) || !catalog.Published || !catalog.SupportedBuilder)
                        return Task.FromResult(command.UpdateFailed("PARAM.OPTION_CHAIN_CATALOG_UNSUPPORTED: an exact published option strategy structure is required."));
                }
            }
            var updated = parameterChange.RejectionReason is not null
                ? command.UpdateFailed(ref errorMsg, parameterChange.RejectionReason)
                : state.Update(command.CreateParameterVersionPublishedEvent(parameterChange), command);
            var result = updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
            return Task.FromResult(result);
        }

        /// <summary>Computes the proposed business change without changing actor state.</summary>
        ///
        /// <param name="state">The authoritative state, read without mutation.</param>
        /// <param name="hash">The request identity hash.</param>
        /// <param name="parameterChange">The proposed business change or rejection reason.</param>
        /// <returns>True when computation succeeds; otherwise false with its business rejection.</returns>
        internal bool Compute(ParameterSetCommandState state, string hash, out ParameterVersionChange parameterChange)
        {
            try
            {
                var parameterVersion = ParameterMutationModel.Decide(command, state.CatalogRevision, state.Versions, DateTime.UtcNow);
                parameterChange = new(parameterVersion, state.CatalogRevision + 1, hash, ParameterAuditModel.ForSet(command, state.Versions, parameterVersion));
                return true;
            }
            catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                parameterChange = new(null, state.CatalogRevision, hash, null, rejection.Message);
                return false;
            }
        }

        /// <summary>Creates the source event with the originating command identity.</summary>
        ///
        /// <param name="parameterChange">The accepted immutable business change.</param>
        /// <returns>The source event ready for state application.</returns>
        internal ParameterVersionPublishedEvent CreateParameterVersionPublishedEvent(ParameterVersionChange parameterChange)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, ParameterVersionPublishedEvent.Actor, ParameterVersionPublishedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                Revision = parameterChange.CatalogRevision,
                RequestHash = parameterChange.RequestHash,
                Version = parameterChange.ParameterVersion,
                Audit = parameterChange.ParameterAudit,
            };
    }
}
