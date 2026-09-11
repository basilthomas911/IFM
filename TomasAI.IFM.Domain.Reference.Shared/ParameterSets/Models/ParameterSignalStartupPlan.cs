using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public enum ParameterSignalProducer { ClosedBars=0, Ema=1, Rsi=2, Atr=3, Adx=4, Macd=5, Bollinger=6, Structure=7, Tdi=8, VxTermStructure=9 }
[MessagePackObject]
public sealed record ParameterSignalProducerKey([property:Key(0)] ParameterSignalProducer Producer,[property:Key(1)] TimeFrameType Interval,[property:Key(2)] int Period=0);
[MessagePackObject]
public sealed record ParameterSignalDemand([property:Key(0)] ParameterSignalProducerKey Key,[property:Key(1)] string Consumer,[property:Key(2)] Guid RequirementId,[property:Key(3)] bool Prepare,[property:Key(4)] bool Monitor,[property:Key(5)] int MaximumAgeSeconds);
[MessagePackObject]
public sealed record ParameterSignalStartupStep([property:Key(0)] ParameterSignalProducerKey Key,[property:Key(1)] bool Prepare,[property:Key(2)] bool Monitor,[property:Key(3)] ParameterSignalDemand[] Consumers);
[MessagePackObject]
public sealed record ParameterSignalStartupPlan([property:Key(0)] Guid StartupRunId,[property:Key(1)] string AssignmentFingerprint,[property:Key(2)] string Fingerprint,[property:Key(3)] ParameterSignalStartupStep[] Steps,[property:Key(4)] string[]? Issues=null);

