using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;

/// <summary>Adapts the existing exact catalog/Portfolio validators into the standard ordered command-error list.</summary>
public static class CompositionCatalogValidation
{
    public static List<ValidationError> ValidateCompositionCatalog(this List<ValidationError> errors, ExecuteOrderCompositionPipelineCommand c)
    {
        if (errors.Count != 0) return errors;
        try
        {
            var selected = TradeSelectionContracts.ReadResult(c.AcceptedSelectionEnvelope);
            var resolved = CompositionBindingResolver.Resolve(selected, c.SelectionBinding, c.CompositionBinding.FrozenAtUtc);
            if (resolved.BindingSha256 != c.CompositionBinding.BindingSha256) errors.Add(new("OC.CONFIG.PROFILE_MISMATCH"));
            if (c.SelectionBinding.SchemaVersion == 1)
            {
                if (c.WorkflowView.CompositionHandoff is not { } handoff || c.Reservation is null)
                    errors.Add(new("OC.CONTRACT.RESERVATION_INVALID"));
                else
                    TradeSelectionHandoff.ValidateReservation(handoff, c.Reservation);
            }
            else if (c.Reservation is not null || c.WorkflowView.CompositionHandoff is not null)
                errors.Add(new("OC.CONTRACT.OWNERSHIP_PREMATURE"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            errors.Add(new(ex is CompositionException cex ? cex.ReasonCode : "OC.CONTRACT.UPSTREAM_INVALID"));
        }
        return errors;
    }
}
