using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Mapped Fund authorization handling; the shared financial fence verifies the committed reservation before append.</summary>
public static class AuthorizeFundOrderRisk
{
    public static List<ValidationError> ValidateFundRiskAuthorization(this List<ValidationError> errors,
        AuthorizeFundOrderRiskCommand command)
    {
        var body = command;
        if (body?.OrderId is null || command.EntityId is null || body.Authorization is null || body.ExpectedVersion <= 0 ||
            body.OrderId.PortfolioId != command.EntityId.PortfolioId || body.OrderId.FundId != command.EntityId.FundId ||
            body.OrderId.OrderId <= 0 || body.Authorization.PortfolioId != command.EntityId.PortfolioId ||
            body.Authorization.FundId != command.EntityId.FundId || body.Authorization.OrderId != body.OrderId.OrderId)
            errors.Add(new("Exact Fund order identity and version are required."));
        else
        {
            try { body.Authorization.Validate(); }
            catch (ArgumentException ex) { errors.Add(new(ex.Message)); }
        }
        return errors;
    }

    public static IPortfolioFundDomainEvent Execute(this AuthorizeFundOrderRiskCommand command,
        PortfolioFundAggregate state, DateTime now, string principal)
        => state.AuthorizeRisk(command.CommandId, state.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, command.Authorization, now, principal);
}
