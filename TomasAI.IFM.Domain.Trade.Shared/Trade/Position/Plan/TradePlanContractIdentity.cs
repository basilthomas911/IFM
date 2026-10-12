using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

public static class TradePlanContractIdentity
{
    public static string Fingerprint(this UpdateIronCondorTradePlanCommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.IronCondorTradePlanInputs is null
            ? Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId)
            : Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(command.IronCondorTradePlanInputs)
                .Concat(Encoding.UTF8.GetBytes(Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId))).ToArray()));
    }

    public static string Fingerprint(this UpdateVerticalSpreadTradePlanCommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        return Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId);
    }

    public static string Fingerprint(this UpdateFuturesTradePlanCommand command) 
    {
        ArgumentNullException.ThrowIfNull(command);
        return Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId);
    }

    public static Guid DeterministicId(string scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scope));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    public static string PlanHash(StrategyTradePlanSnapshot plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var value = string.Create(CultureInfo.InvariantCulture,
            $"{plan.Position.Id.Format()}|{plan.Position.PositionSequence}|{plan.CurrentValue}|{plan.TotalPnl}|{plan.ForwardTradePrice}|{plan.ForwardPnl}|{plan.State}|{plan.Action}|{plan.ReasonCode}|{plan.Parameters.Version}");
        var bytes = Encoding.UTF8.GetBytes(value);
        if (plan.IronCondorTradePlanSnapshot is not null)
            bytes = bytes.Concat(MessagePackSerializer.Serialize(plan.IronCondorTradePlanSnapshot)).ToArray();
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    static string Fingerprint(string id, StrategyPositionSnapshot position, TradePlanParameters parameters, Guid sourceEventId)
    {
        var value = string.Create(CultureInfo.InvariantCulture,
            $"{id}|{position.PositionSequence}|{position.RouteGeneration}|{position.AsOfUtc:O}|{parameters.Version}|{sourceEventId:N}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
