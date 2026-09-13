using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command;

public static class ChangeTradeLegData
{
    public static ServiceResult<GuidResult> Execute(
        this ChangeTradeLegDataCommand command,
        VerticalSpreadPositionCommandState state) =>
        VerticalSpreadPositionTransition.Apply(
            command,
            state,
            machine => machine.UpdateLeg(
                command.TradeLegId,
                command.ContractId,
                command.Price,
                command.SourceSequence,
                command.EffectiveAtUtc,
                command.RouteGeneration));
}
