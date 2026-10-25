using MessagePack;
using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
/// <summary>Exact DTO and retry-identity oracles; no Portfolio is persisted.</summary>
internal static class PortfolioFixtures {
 public static readonly Guid Key=Guid.Parse("c97dbde0-3a89-46f4-b6f3-0750881736a3");
 public static readonly PortfolioReadModel Portfolio=new(){PortfolioId=1,Name="Native fixture portfolio",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Active,EffectiveFromUtc=StrategyFixtures.At,CreatedOnUtc=StrategyFixtures.At,CreatedBy="Fixture",ActivePolicyId=11,ActivePolicyVersion=1,BrokerAccountRefs=["paper:fixture"]};
 public static readonly FundMandateReadModel Fund=new(){PortfolioId=1,FundId=2,FundCode="native-fixture",Name="Native fixture fund",FundMandateVersion=1,OperatingState=FundOperatingState.Active,TradingYear=2026,EffectiveFromUtc=StrategyFixtures.At,CreatedOnUtc=StrategyFixtures.At,CreatedBy="Fixture",DecisionHorizon="Daily",Objective="Native parity fixture",UnderlyingUniverse=["ES"],EligibleAssetTypes=["FuturesOption"],PermittedDirections=["Long","Short"],PermittedConditions=["Normal"],PermittedTradeFamilies=["IronCondor"]};
 public static readonly PortfolioFinancialPolicyReadModel Policy=new(){PortfolioId=1,PolicyId=11,PolicyVersion=1,Name="Fixture risk policy",OperatingState=PortfolioFinancialPolicyState.Active,CapitalBase=50000m,ProtectedReserve=5000m,MaximumDeployableCapital=45000m,MaximumRiskPerTrade=1000m,MaximumAggregateRisk=5000m,MaximumMargin=10000m,MaximumGrossNotional=1000000m,MaximumOpenPositions=5,MaximumDrawdownAmount=10000m,EffectiveFromUtc=StrategyFixtures.At,CreatedOnUtc=StrategyFixtures.At,CreatedBy="Fixture",AggregateRevision=5,TradeFamilyLimits=[new(){TradeStrategyFamilyId=99,DefinitionVersion=2,Enabled=true,MaximumRiskPerTrade=1000m,MaximumAggregateRisk=5000m,MaximumMargin=10000m,MaximumGrossNotional=1000000m,MaximumOpenPositions=5}]};
 public static void Export(string output){
  var templates=typeof(PortfolioReadModel).Assembly.GetTypes().Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters&&t.Namespace?.StartsWith("TomasAI.IFM.Application.Contracts.Portfolio",StringComparison.Ordinal)==true&&t.GetConstructor(Type.EmptyTypes) is not null&&t.GetField("Actor",BindingFlags.Public|BindingFlags.Static) is not null&&t.GetCustomAttribute<MessagePackObjectAttribute>() is not null)
   .ToDictionary(t=>t.FullName!,t=>MessagePackSerializer.Serialize(t,Activator.CreateInstance(t),MessagePackBinarySerializer.ContentOptions).Select(v=>(int)v).ToArray());
  FixtureFiles.WriteAllText(Path.Combine(output,"portfolio-request-defaults.json"),JsonSerializer.Serialize(templates));
  // DTO initializers are part of editor behavior (currency, schema and empty
  // collections). Export them rather than guessing Rust defaults.
  var models=typeof(PortfolioReadModel).Assembly.GetTypes().Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters&&(t.Namespace==typeof(PortfolioReadModel).Namespace||t.Namespace=="TomasAI.IFM.Domain.Portfolio.Shared.Financial")&&t.GetConstructor(Type.EmptyTypes) is not null&&t.GetCustomAttribute<MessagePackObjectAttribute>() is not null)
   .ToDictionary(t=>t.FullName!,t=>MessagePackSerializer.Serialize(t,Activator.CreateInstance(t),MessagePackBinarySerializer.ContentOptions).Select(v=>(int)v).ToArray());
  FixtureFiles.WriteAllText(Path.Combine(output,"portfolio-model-defaults.json"),JsonSerializer.Serialize(models));
  var command=new CreatePortfolioCommand(Portfolio,Key);
  var type=typeof(TomasAI.IFM.Application.Api.Nats.Client.PortfolioCommandApi).Assembly.GetType("TomasAI.IFM.Application.Api.Nats.Client.IdempotentCommandId")!;
  var id=(Guid)type.GetMethod("Create",BindingFlags.Public|BindingFlags.Static)!.MakeGenericMethod(typeof(PortfolioReadModel)).Invoke(null,[Key,Portfolio])!;
  FixtureFiles.WriteAllBytes(Path.Combine(output,"portfolio-create-input.msgpack"),MessagePackSerializer.Serialize(Portfolio));
  var normalized=command with{CommandId=id,Subject=new(TomasAI.IFM.Shared.EventModelActor.ActorType.Command,CreatePortfolioCommand.Actor,CreatePortfolioCommand.Verb,"1"),EntityId=new(1),ErrorCode=34002,CorrelationId=Key,RequestedOnUtc=StrategyFixtures.At,Access=TomasAI.IFM.Domain.Portfolio.Shared.Common.PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}")};
  FixtureFiles.WriteAllBytes(Path.Combine(output,"portfolio-create-normalized.msgpack"),MessagePackSerializer.Serialize(normalized,MessagePackBinarySerializer.ContentOptions));
  FixtureFiles.WriteAllBytes(Path.Combine(output,"portfolio-fund.msgpack"),MessagePackSerializer.Serialize(Fund,MessagePackBinarySerializer.Options));
  FixtureFiles.WriteAllText(Path.Combine(output,"portfolio-create-identity.json"),JsonSerializer.Serialize(new{key=Key,commandId=id}));
  FixtureFiles.WriteAllText(Path.Combine(output,"portfolio-fields-display.json"),JsonSerializer.Serialize(TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Fields(Portfolio,enumNames:true)));
  FixtureFiles.WriteAllText(Path.Combine(output,"portfolio-fund-fields-display.json"),JsonSerializer.Serialize(TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Fields(Fund,enumNames:true)));
 }
}
