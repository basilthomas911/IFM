using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle;

/// <summary>Closed-by-default operator authorization with an explicit immutable allow-list.</summary>
public sealed class SupervisorOperatorAuthorizer(IEnumerable<string> allowedOperators) : ISupervisorOperatorAuthorizer
{
    readonly HashSet<string> _allowed = new(allowedOperators ?? [], StringComparer.OrdinalIgnoreCase);

    public bool IsAuthorized(string requester, SupervisorActorOperationKind operation)
        => !string.IsNullOrWhiteSpace(requester) && _allowed.Contains(requester);
}
