using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Identities;

/// <summary>Identifies one Portfolio aggregate.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioId : IActorEntityId
{
    [Key(0)] public int Id { get; init; }

    public PortfolioId() { }

    public PortfolioId(int id) => Id = id;

    public string Format() => Id.ToString(CultureInfo.InvariantCulture);

    public IReadOnlyList<string> Validate() =>
        Id > 0 ? [] : ["PortfolioId.Id must be greater than zero."];

    public override string ToString() => Format();
}

/// <summary>Identifies one Portfolio-owned financial policy aggregate.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFinancialPolicyId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int PolicyId { get; init; }

    public PortfolioFinancialPolicyId() { }
    public PortfolioFinancialPolicyId(int portfolioId, int policyId) =>
        (PortfolioId, PolicyId) = (portfolioId, policyId);
    public string Format() => FormattableString.Invariant($"{PortfolioId}.{PolicyId}");
    public IReadOnlyList<string> Validate() => PortfolioId > 0 && PolicyId > 0
        ? [] : ["PortfolioId and PolicyId must be greater than zero."];
    public override string ToString() => Format();
}

/// <summary>Identifies one Fund mandate within its Portfolio.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }

    public PortfolioFundId() { }

    public PortfolioFundId(int portfolioId, int fundId) =>
        (PortfolioId, FundId) = (portfolioId, fundId);

    public string Format() => FormattableString.Invariant($"{PortfolioId}.{FundId}");

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioFundId.PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("PortfolioFundId.FundId must be greater than zero.");
        return errors;
    }

    public override string ToString() => Format();
}

/// <summary>Identifies one planned FundOrder within its Portfolio and Fund.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundOrderId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }

    public PortfolioFundOrderId() { }

    public PortfolioFundOrderId(int portfolioId, int fundId, int orderId) =>
        (PortfolioId, FundId, OrderId) = (portfolioId, fundId, orderId);

    public string Format() => FormattableString.Invariant($"{PortfolioId}.{FundId}.{OrderId}");

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioFundOrderId.PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("PortfolioFundOrderId.FundId must be greater than zero.");
        if (OrderId <= 0) errors.Add("PortfolioFundOrderId.OrderId must be greater than zero.");
        return errors;
    }

    public override string ToString() => Format();
}

/// <summary>Identifies one planned FundOrderTrade.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundOrderTradeId : IActorEntityId
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public int TradeId { get; init; }

    public PortfolioFundOrderTradeId() { }

    public PortfolioFundOrderTradeId(int portfolioId, int fundId, int orderId, int tradeId) =>
        (PortfolioId, FundId, OrderId, TradeId) = (portfolioId, fundId, orderId, tradeId);

    public string Format() => FormattableString.Invariant($"{PortfolioId}.{FundId}.{OrderId}.{TradeId}");

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (PortfolioId <= 0) errors.Add("PortfolioFundOrderTradeId.PortfolioId must be greater than zero.");
        if (FundId <= 0) errors.Add("PortfolioFundOrderTradeId.FundId must be greater than zero.");
        if (OrderId <= 0) errors.Add("PortfolioFundOrderTradeId.OrderId must be greater than zero.");
        if (TradeId <= 0) errors.Add("PortfolioFundOrderTradeId.TradeId must be greater than zero.");
        return errors;
    }

    public override string ToString() => Format();
}
