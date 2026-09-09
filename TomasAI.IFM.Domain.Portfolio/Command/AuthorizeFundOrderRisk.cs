using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Mapped Fund authorization handling; the shared financial fence verifies the committed reservation before append.</summary>
public static class AuthorizeFundOrderRisk
{
    public static List<ValidationError> ValidateFundRiskAuthorization(this List<ValidationError> errors,
        PortfolioCommand<AuthorizeFundOrderRiskPayload, PortfolioFundId> command)
    {
        var body = command.Payload;
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

    public static PortfolioFundDomainEvent Execute(this PortfolioCommand<AuthorizeFundOrderRiskPayload, PortfolioFundId> command,
        PortfolioFundAggregate state, DateTime now, string principal)
        => state.AuthorizeRisk(command.CommandId, state.Revision, command.Payload.OrderId.OrderId,
            command.Payload.ExpectedVersion, command.Payload.Authorization, now, principal);
}
