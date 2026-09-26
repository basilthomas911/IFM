using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

public enum TradePlanState : byte
{
    Normal = 1,
    Hold = 2,
    Warning = 3,
    Breached = 4,
    ExitRequired = 5,
    CalculationFailed = 6,
    Closed = 7
}

public enum TradePlanAction : byte
{
    None = 0,
    Monitor = 1,
    Hold = 2,
    ExitAtLimit = 3,
    ExitAtMarket = 4
}

[MessagePackObject]
public sealed record TradePlanParameters
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public decimal MaximumLoss { get; init; } = 1_000m;
    [Key(2)] public decimal WarningLoss { get; init; } = 750m;
    [Key(3)] public decimal ProfitTarget { get; init; } = 500m;
    [Key(4)] public decimal MaterialPnlChange { get; init; } = 10m;
    [Key(5)] public decimal MaterialPriceChange { get; init; } = 0.25m;
    [Key(6)] public int MaximumDataAgeSeconds { get; init; } = 30;

    public void Validate()
    {
        if (Version < 1 || MaximumLoss <= 0 || WarningLoss <= 0 || WarningLoss > MaximumLoss ||
            ProfitTarget <= 0 || MaterialPnlChange < 0 || MaterialPriceChange < 0 || MaximumDataAgeSeconds < 1)
            throw new ArgumentException("Valid Trade Plan parameters are required.");
    }
}

[MessagePackObject]
public sealed record StrategyTradePlanSnapshot
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public long PlanRevision { get; init; }
    [Key(4)] public decimal OpeningValue { get; init; }
    [Key(5)] public decimal CurrentValue { get; init; }
    [Key(6)] public decimal TotalPnl { get; init; }
    [Key(7)] public decimal ForwardTradePrice { get; init; }
    [Key(8)] public decimal ForwardPnl { get; init; }
    [Key(9)] public decimal ForwardLoss { get; init; }
    [Key(10)] public TradePlanState State { get; init; }
    [Key(11)] public TradePlanAction Action { get; init; }
    [Key(12)] public bool RequiresExit { get; init; }
    [Key(13)] public bool MaterialChange { get; init; }
    [Key(14)] public string ReasonCode { get; init; } = string.Empty;
    [Key(15)] public string Explanation { get; init; } = string.Empty;
    [Key(16)] public TradePlanParameters Parameters { get; init; } = new();
    [Key(17)] public string ContentHash { get; init; } = string.Empty;
    [Key(18)] public DateTime CalculatedAtUtc { get; init; }
}

[MessagePackObject]
public readonly record struct IronCondorTradePlanId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Position.Format()}.{ValueDate:yyyyMMdd}.IronCondor");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default;
}

[MessagePackObject]
public readonly record struct VerticalSpreadTradePlanId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Position.Format()}.{ValueDate:yyyyMMdd}.VerticalSpread");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default;
}

[MessagePackObject]
public readonly record struct FuturesTradePlanId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Position.Format()}.{ValueDate:yyyyMMdd}.Futures");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default;
}

public static class TradePlanContractIdentity
{
    public static string Fingerprint(this UpdateIronCondorTradePlanCommand command) =>
        Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId);

    public static string Fingerprint(this UpdateVerticalSpreadTradePlanCommand command) =>
        Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId);

    public static string Fingerprint(this UpdateFuturesTradePlanCommand command) =>
        Fingerprint(command.EntityId.Format(), command.Position, command.Parameters, command.SourceEventId);

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
        var value = string.Create(CultureInfo.InvariantCulture,
            $"{plan.Position.Id.Format()}|{plan.Position.PositionSequence}|{plan.CurrentValue}|{plan.TotalPnl}|{plan.ForwardTradePrice}|{plan.ForwardPnl}|{plan.State}|{plan.Action}|{plan.ReasonCode}|{plan.Parameters.Version}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    static string Fingerprint(string id, StrategyPositionSnapshot position, TradePlanParameters parameters, Guid sourceEventId)
    {
        var value = string.Create(CultureInfo.InvariantCulture,
            $"{id}|{position.PositionSequence}|{position.RouteGeneration}|{position.AsOfUtc:O}|{parameters.Version}|{sourceEventId:N}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
