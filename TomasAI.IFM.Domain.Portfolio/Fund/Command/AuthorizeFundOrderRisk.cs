using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Mapped Fund authorization handling; the shared financial fence verifies the committed reservation before append.</summary>
public static class AuthorizeFundOrderRisk
{
    /// <summary>Validates exact Fund order ownership, version and financial authorization evidence before audit.</summary>
    /// <param name="errors">The accumulated ingress validation failures.</param>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <returns>The operation result.</returns>
    public static List<ValidationError> ValidateFundRiskAuthorization(this List<ValidationError> errors,
        AuthorizeFundOrderRiskCommand command)
    {
        var body = command;
        if (body?.OrderId is null || command.EntityId is null || body.Authorization is null || body.ExpectedVersion <= 0 ||
            body.OrderId.PortfolioId != command.EntityId.PortfolioId || body.OrderId.FundId != command.EntityId.FundId ||
            body.OrderId.OrderId <= 0 || body.Authorization.PortfolioId != command.EntityId.PortfolioId ||
            body.Authorization.FundId != command.EntityId.FundId || body.Authorization.OrderId != body.OrderId.OrderId)
            errors.Add(new("Exact Fund order identity and version are required."));
        errors.ValidateFundRiskAuthorization(command.Authorization);
        return errors;
    }


    /// <summary>Computes and guards the business change, then applies one source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    public static ServiceResult<GuidResult> Execute(this AuthorizeFundOrderRiskCommand command,
        PortfolioFundAggregate state, DateTime now, string principal)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FundCompositionStateChanged event";
        var updated = command.Compute(state, now, principal, out var fundChange) switch
        {
            _ when !fundChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{fundChange.RejectionCode};{fundChange.RejectionReason}"),
            _ when fundChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed command identity does not match the originating command"),
            _ when fundChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed aggregate revision is not the next revision"),
            _ => state.Update(command.CreateFundCompositionStateChangedEvent(fundChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }
    /// <summary>Computes immutable business values without changing authoritative state.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this AuthorizeFundOrderRiskCommand command, PortfolioFundAggregate state, DateTime now, string principal, out FundCompositionStateChangedCompute fundChange)
    {
        try
        {
            fundChange = (FundCompositionStateChangedCompute)state.ComputeAuthorizeRisk(command.CommandId, state.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, command.Authorization, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            fundChange = new FundCompositionStateChangedCompute { Accepted = false, RejectionCode = "PortfolioFund.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static FundCompositionStateChangedEvent CreateFundCompositionStateChangedEvent(this AuthorizeFundOrderRiskCommand command, FundCompositionStateChangedCompute fundChange) => new()
    {
        Id = fundChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = fundChange.OccurredOnUtc,
        Revision = fundChange.Revision,
        OccurredOnUtc = fundChange.OccurredOnUtc,
        Principal = fundChange.Principal,
        OriginatedOnUtc = fundChange.OccurredOnUtc,
        Order = fundChange.FundOrder,
    };
}
