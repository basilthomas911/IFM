using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record FundProfitLossOrdersReadModel
{
    [Key(0)]
    public FundOrderAmountReadModel ProfitOrderAmount { get; init; }

    [Key(1)]
    public FundOrderAmountReadModel LossOrderAmount { get; init; }

    public FundProfitLossOrdersReadModel() { }

    [SerializationConstructor]
    public FundProfitLossOrdersReadModel(FundOrderAmountReadModel profitOrderAmount, FundOrderAmountReadModel lossOrderAmount)
    {
        ProfitOrderAmount = profitOrderAmount;
        LossOrderAmount = lossOrderAmount;
    }

    [IgnoreMember]
    public bool IsValid => (ProfitOrderAmount?.IsValid ?? false) || (LossOrderAmount?.IsValid ?? false);
}
