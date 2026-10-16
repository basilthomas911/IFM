using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public static class OptionSpreadStrategyParameterScopeModel
{
    public const string ConsumerKind="market-selection";
    public const string ConsumerId="option-spread-strategy";
    public static ParameterAssignmentScope IronCondor()=>Create(IronCondorMarketSelectionParameterModel.ComponentCode,"iron-condor-defaults","IronCondor");
    public static ParameterAssignmentScope VerticalSpread()=>Create(VerticalSpreadMarketSelectionParameterModel.ComponentCode,"vertical-spread-defaults","VerticalSpread");
    static ParameterAssignmentScope Create(string component,string role,string strategy)=>
        new(ConsumerKind,ConsumerId,role,component,string.Empty,ParameterBinaryFingerprint.Hash((ConsumerKind,ConsumerId,role,component,strategy))){Strategy=strategy};
    public static void Validate(ParameterAssignmentScope scope)
    {
        var value=ParameterHistoryReader.UpgradeScope(scope);
        var expected=value.ComponentCode switch
        {
            IronCondorMarketSelectionParameterModel.ComponentCode=>IronCondor(),
            VerticalSpreadMarketSelectionParameterModel.ComponentCode=>VerticalSpread(),
            _=>throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED")
        };
        var hash=value.Codec==ParameterValueCodec.Codec?expected.ScopeSha256:value.ComponentCode==IronCondorMarketSelectionParameterModel.ComponentCode?
            "e48182571bb02974339e5cd6135d6ad64fa19acd314c5a5841f1b701f4b01eda":"af043bbcd4523653c93c70c99253a249c6191527f7fa353846ce42503137ef56";
        if(value.ConsumerKindCode!=ConsumerKind||value.ConsumerId!=ConsumerId||value.Role!=expected.Role||value.Strategy!=expected.Strategy||value.TargetHorizon is not null||value.ScopeSha256!=hash)
            throw new ArgumentException("PARAM.SCOPE_INVALID");
    }
    public static Guid AssignmentId(ParameterAssignmentScope scope)
    {Validate(scope);return Guid.Parse(scope.ComponentCode==IronCondorMarketSelectionParameterModel.ComponentCode?"445adcc2-2cbd-f743-df4d-77858a834ca2":"740c1a9c-5da1-68b3-b506-c163e030380a");}
}
