using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetFundMaxProfitGeneratedQuery : IQuery<FundMaxProfitGeneratedReadModel>
{
    [IgnoreMember] public const string Actor = "FundQuery";
    [IgnoreMember] public const string Verb = "GetFundMaxProfitGenerated";
    [IgnoreMember] public const int ErrorId = 1006;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string QueryParams { get; init; }

    /// <summary>
    ///  
    /// </summary>
    [Key(2)]
    public int  FundId { get; init; }

    [Key(3)]
    public DateOnly TradeDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetFundMaxProfitGeneratedQuery() { }

    public GetFundMaxProfitGeneratedQuery(int fundId, DateOnly tradeDate)
    {
        FundId = fundId;
        TradeDate = tradeDate;
        EntityId = new GetFundMaxProfitGeneratedParameter(fundId, tradeDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFundMaxProfitGeneratedQuery(
        ActorSubject subject,         // Key(0)
        IActorEntityId entityId,      // Key(1)
        int fundId,                    // Key(4)
        DateOnly tradeDate)            // Key(5)
    {
        Subject = subject;
        EntityId = new GetFundMaxProfitGeneratedParameter(fundId, tradeDate);
        FundId = fundId;
        TradeDate = tradeDate;
        ErrorCode = ErrorId;
    }

}
