using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Preparation;

[MessagePackObject(AllowPrivate = true)]
public sealed record ValidatePortfolioReferenceCommand : ICommand<ActorEntityId>
{
    public const string Actor = "PortfolioReferenceValidationFunction";
    public const string Verb = "ValidatePortfolioReference";
    public const int ErrorId = 34000;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ActorEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.PortfolioBoundedContext;
    [Key(6)] public PortfolioAccessContext Access { get; init; } = default!;
    [Key(7)] public FundMandateReadModel? Mandate { get; init; }
    [Key(8)] public FundTradeTemplateAssignmentReadModel? Assignment { get; init; }
    [Key(9)] public PortfolioFinancialPolicyReadModel? Policy { get; init; }
    [Key(10)] public bool ActivatesPolicy { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the typed MessagePack projection array.")]
    [Key(11)] public FundTradeTemplateAssignmentReadModel[]? ActivationAssignments { get; init; }
    [Key(12)] public Guid OwnerCommandId { get; init; }
    [IgnoreMember] public string CommandName => nameof(ValidatePortfolioReferenceCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => string.Empty;
    [IgnoreMember] public bool RequiresEvidence => Mandate is { SchemaVersion: >= 3 } ||
        Mandate is { PermittedTradeStrategyFamilies.Length: > 0 } || Assignment?.TradeStrategyFamily is not null ||
        Policy is { TradeFamilyLimits.Length: > 0 } || ActivationAssignments is not null;

    public string ComputeInputHash()
    {
        var policy = Policy is null ? null : new
        {
            Policy.PortfolioId, Policy.PolicyId, Policy.PolicyVersion,
            Limits = Policy.TradeFamilyLimits.Select(limit => new { limit.CatalogDeployment, limit.Enabled })
                .OrderBy(limit => limit.CatalogDeployment?.Id).ThenBy(limit => limit.CatalogDeployment?.Version)
                .ThenBy(limit => limit.Enabled).ToArray()
        };
        var activationDeployments = ActivationAssignments?.Select(assignment => assignment.TradeStrategyFamily?.CatalogDeployment)
            .OrderBy(deployment => deployment?.Id).ThenBy(deployment => deployment?.Version).ToArray();
        return FinancialCanonicalHash.Compute(new { OwnerCommandId, Access, Mandate, Assignment, Policy = policy, ActivatesPolicy,
            ActivationDeployments = activationDeployments });
    }

    public static ValidatePortfolioReferenceCommand ForOwnerCommand(Guid ownerCommandId, Guid? operationId = null)
        => ForOperation(operationId ?? Guid.NewGuid()) with { OwnerCommandId = ownerCommandId };

    public static ValidatePortfolioReferenceCommand ForOperation(Guid operationId)
    {
        var id = new ActorEntityId(operationId.ToString("N"));
        return new() { CommandId = operationId, EntityId = id, Subject = new(ActorType.Function, Actor, Verb, id.Format()) };
    }
}
