using TomasAI.IFM.Domain.Supervisor.Lifecycle;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;

namespace TomasAI.IFM.Domain.Supervisor.UnitTests;

public sealed class SupervisorOperatorAuthorizerTests
{
    [Fact]
    public void Is_closed_by_default()
    {
        var authorizer = new SupervisorOperatorAuthorizer([]);
        Assert.False(authorizer.IsAuthorized("trader", SupervisorActorOperationKind.Restart));
    }

    [Fact]
    public void Allows_only_explicit_operator_identity_case_insensitively()
    {
        var authorizer = new SupervisorOperatorAuthorizer(["desk-operator"]);
        Assert.True(authorizer.IsAuthorized("DESK-OPERATOR", SupervisorActorOperationKind.Restart));
        Assert.False(authorizer.IsAuthorized("other", SupervisorActorOperationKind.Restart));
        Assert.False(authorizer.IsAuthorized(string.Empty, SupervisorActorOperationKind.Restart));
    }
}
