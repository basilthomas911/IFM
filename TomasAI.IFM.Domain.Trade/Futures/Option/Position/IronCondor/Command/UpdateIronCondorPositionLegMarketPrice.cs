using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;

public static class UpdateIronCondorPositionLegMarketPrice
{
    public static ServiceResult<GuidResult> Execute(
        this UpdateIronCondorPositionLegMarketPriceCommand command,
        IronCondorPositionCommandState state) =>
        IronCondorPositionTransition.Apply(
            command,
            state,
            machine => machine.UpdateLeg(
                command.TradeLegId,
                command.Price,
                command.SourceSequence,
                command.EffectiveAtUtc,
                command.RouteGeneration));
}
