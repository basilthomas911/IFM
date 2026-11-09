using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.ReferencePreparation.Function;

public static class ValidatePortfolioReference
{
    extension(ValidatePortfolioReferenceCommand request)
    {
        public List<ValidationError> Validate()
        {
            var payloadCount = (request.Mandate is null ? 0 : 1) + (request.Assignment is null ? 0 : 1) + (request.Policy is null ? 0 : 1);
            return request.CommandId == Guid.Empty
                || request.OwnerCommandId == Guid.Empty || request.CommandId == request.OwnerCommandId
                || request.EntityId != new ActorEntityId(request.CommandId.ToString("N"))
                || request.Subject != ValidatePortfolioReferenceCommand.ForOperation(request.CommandId).Subject
                || request.Access is null || payloadCount > 1 || (payloadCount == 0 && request.ActivationAssignments is null)
                || (request.ActivatesPolicy && request.Policy is null)
                || (request.ActivationAssignments is not null && (request.Policy is not null || request.Assignment is not null))
                ? [new ValidationError("Invalid Portfolio Reference validation request.")]
                : [];
        }

        public async ValueTask<PortfolioReferenceValidationCompletedEvent> ExecuteAsync(IReferenceQueryApi references,
            IPortfolioOperationalGuard guard, CancellationToken token, TimeProvider? timeProvider = null)
        {
            guard.Demand(request.Policy is null ? PortfolioOperation.AdministerFund : PortfolioOperation.AdministerPortfolio, request.Access, mutation: true);
            await request.ValidateReferencesAsync(references, token).ConfigureAwait(false);
            return new()
            {
                Subject = new(ActorType.Event, "PortfolioReferenceValidationEvent", "Completed", request.EntityId.Format()),
                Id = request.CommandId, CommandId = request.CommandId, EntityId = request.EntityId,
                EventSource = request.EventSource, ReceivedOn = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime, InputHash = request.ComputeInputHash()
            };
        }

        internal async Task ValidateReferencesAsync(IReferenceQueryApi references, CancellationToken token)
        {
            if (request.ActivationAssignments is { } activating)
            {
                foreach (var enabledAssignment in activating)
                {
                    var deployment = enabledAssignment.TradeStrategyFamily?.CatalogDeployment
                        ?? throw new InvalidOperationException("Legacy assignments must be replaced before activating a Fund.");
                    await StrategyCatalogPermissionValidation.ValidateDeploymentAsync(references, deployment, true, token).ConfigureAwait(false);
                }
            }
            if (request.Policy is { } policy)
            {
                foreach (var limit in policy.TradeFamilyLimits)
                {
                    var key = limit.CatalogDeployment ?? throw new ArgumentException("Legacy risk limits are read-only. Create a policy version with explicit ConfigurationDb deployment limits.", nameof(request));
                    await StrategyCatalogPermissionValidation.ValidateDeploymentAsync(references, key, request.ActivatesPolicy && limit.Enabled, token).ConfigureAwait(false);
                }
                return;
            }

            var mandate = request.Mandate;
            var assignment = request.Assignment;
            if (mandate is { SchemaVersion: >= 3 })
            {
                var selections = await FundSelectionCatalog.LoadAsync(references, token).ConfigureAwait(false);
                selections.ValidateSelections(mandate.UnderlyingUniverse, mandate.EligibleAssetTypes, mandate.PermittedDirections, mandate.PermittedConditions);
            }
            var families = mandate?.PermittedTradeStrategyFamilies ?? (assignment?.TradeStrategyFamily is { } family ? [family] : []);
            if (families.Length == 0) return;
            foreach (var reference in families)
            {
                var key = reference.CatalogDeployment ?? throw new ArgumentException("Legacy family permissions are read-only. Select an exact ConfigurationDb deployment.", nameof(request));
                var row = await StrategyCatalogPermissionValidation.ValidateDeploymentAsync(references, key,
                    assignment?.Enabled == true || mandate?.OperatingState == FundOperatingState.Active, token).ConfigureAwait(false);
                if (mandate is not null && (!mandate.PermittedTradeFamilies.Contains(row.Definition.Code, StringComparer.Ordinal) ||
                    !string.Equals(mandate.DecisionHorizon, row.Definition.Horizon.ToString(), StringComparison.Ordinal)))
                    throw new ArgumentException("Fund deployment classification or horizon does not match its exact reference.", nameof(request));
                if (assignment is null) continue;
                if (!string.Equals(assignment.TradeFamily, row.Definition.Code, StringComparison.Ordinal) ||
                    !string.Equals(assignment.DecisionHorizon, row.Definition.Horizon.ToString(), StringComparison.Ordinal) ||
                    assignment.UnderlyingUniverse.Except(row.Definition.Products.Select(product => product.Symbol), StringComparer.Ordinal).Any())
                    throw new ArgumentException("Assignment classification, horizon or product universe does not match the exact deployment.", nameof(request));
                if (assignment.TradeTemplateId != key.Id || assignment.TradeTemplateVersion != key.Version)
                    throw new ArgumentException("Assignment template identity must equal its ConfigurationDb deployment identity.", nameof(request));
                var selection = row.Definition.PipelineParameters.Where(binding => binding.Kind == CatalogPipelineParameterKind.TradeSelection).ToArray();
                var composition = row.Definition.PipelineParameters.Where(binding => binding.Kind == CatalogPipelineParameterKind.OrderComposition).ToArray();
                if (selection.Length > 1 || composition.Length > 1)
                    throw new ArgumentException("Assignment requires one unambiguous profile per pipeline stage.", nameof(request));
                if (assignment.TradeSelectionHintProfileId != (selection.SingleOrDefault()?.Id ?? Guid.Empty) ||
                    assignment.TradeSelectionHintProfileVersion != (selection.SingleOrDefault()?.Version ?? 0) ||
                    assignment.OrderCompositionProfileId != (composition.SingleOrDefault()?.Id ?? Guid.Empty) ||
                    assignment.OrderCompositionProfileVersion != (composition.SingleOrDefault()?.Version ?? 0))
                    throw new ArgumentException("Assignment profiles must match the exact deployment bindings.", nameof(request));
            }
        }
    }

    extension(FunctionEventContext<ValidatePortfolioReferenceCommand> input)
    {
        public PortfolioReferenceValidationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "PortfolioReferenceValidationEvent", "Failed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = input.Exception switch
            {
                PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
                PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
                _ => PortfolioErrorCodes.ValidationFailed
            },
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.Exception?.Message ?? "Portfolio Reference validation failed."
        };
    }
}
