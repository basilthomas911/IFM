using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents the balance of a fund for display in a user interface or API response.
/// </summary>
[MessagePackObject(false)]
public record FundBalanceReadModel
{
    [Key(0)] public decimal Value { get; init; }

    public FundBalanceReadModel() { }

    [SerializationConstructor]
    public FundBalanceReadModel(decimal value)
    {
        Value = value;
    }

    [IgnoreMember]
    public bool IsValid => Value > 0;
}
