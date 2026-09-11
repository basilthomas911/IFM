using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Preserves current specialist dependencies while allowing optional evidence reductions.</summary>
public sealed class RegimeDiscoveryParameterModel : IParameterComponentDescriptor
{
    public const string ComponentCode = "strategy-workflow.regime-discovery";
    static readonly RegimeDiscoverySignalMetric[] Trend = [RegimeDiscoverySignalMetric.Ema20,
        RegimeDiscoverySignalMetric.Ema50, RegimeDiscoverySignalMetric.Ema200,
        RegimeDiscoverySignalMetric.Ema20Slope, RegimeDiscoverySignalMetric.Ema50Slope,
        RegimeDiscoverySignalMetric.Ema200Slope, RegimeDiscoverySignalMetric.Rsi14,
        RegimeDiscoverySignalMetric.Rsi14Slope, RegimeDiscoverySignalMetric.Adx14,
        RegimeDiscoverySignalMetric.PlusDi14, RegimeDiscoverySignalMetric.MinusDi14,
        RegimeDiscoverySignalMetric.MacdHistogram, RegimeDiscoverySignalMetric.Atr14];
    static readonly RegimeDiscoverySignalMetric[] Structure = [RegimeDiscoverySignalMetric.Atr14,
        RegimeDiscoverySignalMetric.AtrBaselineRatio, RegimeDiscoverySignalMetric.BollingerWidthRatio,
        RegimeDiscoverySignalMetric.BollingerPosition, RegimeDiscoverySignalMetric.Ema20Interaction,
        RegimeDiscoverySignalMetric.AtrNormalizedRange, RegimeDiscoverySignalMetric.RollingHigh20,
        RegimeDiscoverySignalMetric.RollingLow20, RegimeDiscoverySignalMetric.BreakoutDistanceAtr];
    public ParameterComponentSummary Summary => new("strategy-workflow", "Strategy Workflow", ComponentCode,
        "Regime Discovery", [1, 2, 3, 4, ParameterSchemaRegistry.CurrentRegimeSchemaVersion], true);
    public string CreateDraftPayload(Guid setId) => JsonSerializer.Serialize(CreateSeed(setId));
    public static RegimeDiscoveryParameterSet CreateSeed(Guid setId)
    {
        if (setId == Guid.Empty) throw new ArgumentException("Set identity is required.");
        var parameters = RegimeDiscoveryParameterSet.CreateDefault(setId, setId, TimeFrameType.Daily);
        return parameters with { SchemaVersion = 2, SignalRequirements = Defaults(parameters) };
    }
    /// <summary>Creates a horizon-specific current-schema membership draft; legacy drafts remain reproducible through CreateSeed.</summary>
    public static RegimeDiscoveryParameterSet CreateExplicitSeed(Guid setId, TimeFrameType horizon = TimeFrameType.Daily)
    {
        if (setId == Guid.Empty) throw new ArgumentException("Set identity is required.");
        var legacy = RegimeDiscoveryParameterSet.CreateDefault(setId, setId, horizon);
        var parameters = legacy with
        {
            SchemaVersion = ParameterSchemaRegistry.CurrentRegimeSchemaVersion,
            StrategyParameterSetId=Guid.Empty,StrategyParameterSetVersion=0,
            Horizon = horizon != TimeFrameType.Daily
                ? legacy.Horizon with { TimeFrames = legacy.Horizon.TimeFrames.Select(frame => frame with { IsRequired = true }).ToArray() }
                : new()
            {
                TargetHorizon = TimeFrameType.Daily,
                TimeFrames =
                [
                    new() { TimeFrame = TimeFrameType.FifteenSeconds, IsRequired = true, Weight = 1m, MaximumAgeSeconds = 45 },
                    new() { TimeFrame = TimeFrameType.OneMinute, IsRequired = true, Weight = 1m, MaximumAgeSeconds = 180 },
                    new() { TimeFrame = TimeFrameType.FiveMinutes, IsRequired = true, Weight = 1m, MaximumAgeSeconds = 900 }
                ]
            }
        };
        // Keep nonessential catalogue entries editable, but outside the initial set.
        // Inclusion is the only readiness switch in schemas 3+; no included row is optional.
        var catalogueRows = Defaults(parameters)
            .Select(row => row with { Enabled = row.IsRequired, IsRequired = true }).ToArray();
        var signals = NormalizedSignals(parameters, catalogueRows);
        var observations = ObservationDefaults(parameters, catalogueRows);
        return parameters with
        {
            SignalMetrics = signals,
            ObservationMetrics = observations,
            SignalRequirements = RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
                parameters.ParameterSetId, signals, observations, parameters.Horizon)
        };
    }
    /// <summary>Builds a reviewable current-schema working copy without changing the saved source.</summary>
    public static RegimeDiscoveryParameterSet UpgradeToExplicitSet(RegimeDiscoveryParameterSet source, bool rebuildIntervals = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SchemaVersion == ParameterSchemaRegistry.CurrentRegimeSchemaVersion && !rebuildIntervals)
        {
            var completedObservations=ObservationDefaults(source,source.SignalRequirements??[],source.ObservationMetrics);
            if(source.ObservationMetrics is not null&&completedObservations.Length==source.ObservationMetrics.Length)return source;
            if(source.SignalMetrics is null)throw new ArgumentException("The current schema requires normalized signal metrics.");
            return source with
            {
                ObservationMetrics=completedObservations,
                SignalRequirements=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
                    source.ParameterSetId,source.SignalMetrics,completedObservations,source.Horizon)
            };
        }
        if (source.SchemaVersion is not (1 or 2 or 3 or 4 or ParameterSchemaRegistry.CurrentRegimeSchemaVersion) || source.Horizon?.TimeFrames is not { Length: > 0 } ||
            source.Horizon.TimeFrames.Any(frame => frame is null))
            throw new ArgumentException("The source schema and horizon must be valid before upgrading.");
        var target = source with { SchemaVersion = ParameterSchemaRegistry.CurrentRegimeSchemaVersion, Horizon = source.Horizon with
        { TimeFrames = source.Horizon.TimeFrames.Select(frame => frame with { IsRequired = true }).ToArray() } };
        var rows = Defaults(target).Select(row =>
        {
            var previous = source.SignalRequirements?.SingleOrDefault(old => old.Metric == row.Metric && old.TimeFrame == row.TimeFrame);
            return row with
            {
                RequirementId = previous?.RequirementId ?? row.RequirementId,
                Enabled = row.IsRequired || previous?.Enabled == true,
                IsRequired = true,
                MaximumAgeSeconds = previous?.MaximumAgeSeconds ?? row.MaximumAgeSeconds,
                PrepareAtStartup = previous?.PrepareAtStartup ?? row.PrepareAtStartup,
                Monitor = previous?.Monitor ?? row.Monitor
            };
        }).ToArray();
        var signals = NormalizedSignals(target, rows);
        var observations = ObservationDefaults(target, rows,
            source.SchemaVersion>=5?source.ObservationMetrics:null);
        return target with
        {
            SignalMetrics = signals,
            ObservationMetrics = observations,
            SignalRequirements = RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
                target.ParameterSetId, signals, observations, target.Horizon)
        };
    }
    public static RegimeDiscoverySignalConfiguration[] Defaults(RegimeDiscoveryParameterSet parameters)
    {
        var rows = new List<RegimeDiscoverySignalConfiguration>();
        void Add(RegimeDiscoverySignalMetric metric, TimeFrameType frame, bool required, int age)
        {
            var index = rows.FindIndex(x => x.Metric == metric && x.TimeFrame == frame);
            if (index >= 0) { rows[index] = rows[index] with { IsRequired = rows[index].IsRequired || required }; return; }
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{parameters.ParameterSetId:N}:{metric}:{frame}"));
            rows.Add(new() { RequirementId = new Guid(bytes.AsSpan(0,16)), Metric=metric, TimeFrame=frame,
                IsRequired=required, MaximumAgeSeconds=age, CalculationConfigurationId=$"{metric}.v1" });
        }
        foreach (var frame in parameters.Horizon.TimeFrames)
            foreach (var metric in Trend) Add(metric, frame.TimeFrame, frame.IsRequired, frame.MaximumAgeSeconds);
        var target = parameters.Horizon.TimeFrames.Where(x=>x.IsRequired).OrderBy(x=>Duration(x.TimeFrame)).LastOrDefault()
            ?? throw new ArgumentException("At least one required observation frame is needed.");
        foreach(var metric in Structure) Add(metric,target.TimeFrame,true,target.MaximumAgeSeconds);
        Add(RegimeDiscoverySignalMetric.VixLevel, TimeFrameType.Daily,false,345600);
        Add(RegimeDiscoverySignalMetric.VxFrontSecondRatio,TimeFrameType.Daily,true,345600);
        foreach(var frame in parameters.Horizon.TimeFrames) Add(RegimeDiscoverySignalMetric.Tdi,frame.TimeFrame,false,frame.MaximumAgeSeconds);
        foreach(var metric in new[]{RegimeDiscoverySignalMetric.BollingerWidth,RegimeDiscoverySignalMetric.RealizedVolatilityPercentile,
            RegimeDiscoverySignalMetric.PriorVolatilityComposite}) Add(metric,target.TimeFrame,false,target.MaximumAgeSeconds);
        return rows.OrderBy(x=>x.TimeFrame).ThenBy(x=>x.Metric).ToArray();
    }
    public static RegimeDiscoverySignalMetricConfiguration[] NormalizedSignals(
        RegimeDiscoveryParameterSet parameters, IEnumerable<RegimeDiscoverySignalConfiguration> legacyRows)
    {
        return legacyRows.Select(row => (Row: row, Definition: Definition(row.Metric)))
            .Where(value => value.Definition.HasValue)
            .GroupBy(value => (value.Definition!.Value.Metric, value.Row.TimeFrame, value.Definition.Value.PeriodLength))
            .Select(group =>
            {
                var rows = group.Select(value => value.Row).ToArray();
                var key = group.Key;
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"{parameters.ParameterSetId:N}:signal:{key.Metric}:{key.TimeFrame}:{key.PeriodLength}"));
                return new RegimeDiscoverySignalMetricConfiguration
                {
                    RequirementId = new Guid(bytes.AsSpan(0, 16)),
                    Metric = key.Metric,
                    TimeFrame = key.TimeFrame,
                    PeriodLength = key.PeriodLength,
                    Enabled = rows.Any(row => row.Enabled),
                    MaximumAgeSeconds = rows.Max(row => row.MaximumAgeSeconds),
                    PrepareAtStartup = rows.Any(row => row.PrepareAtStartup),
                    Monitor = rows.Any(row => row.Monitor)
                };
            })
            .OrderBy(row => row.TimeFrame).ThenBy(row => row.Metric).ThenBy(row => row.PeriodLength)
            .ToArray();
    }

    public static RegimeDiscoveryObservationMetricConfiguration[] ObservationDefaults(
        RegimeDiscoveryParameterSet parameters, IEnumerable<RegimeDiscoverySignalConfiguration> legacyRows,
        IEnumerable<RegimeDiscoveryObservationMetricConfiguration>? existing = null)
    {
        var existingRows=existing?.ToArray()??[];
        var vxRows = legacyRows.Where(row => row.Metric is RegimeDiscoverySignalMetric.VxFrontSecondRatio
            or RegimeDiscoverySignalMetric.VxFrontLevel).ToArray();
        return Enum.GetValues<ObservationMetricsType>().Where(metric=>metric!=ObservationMetricsType.Unknown)
            .Select(metric=>
            {
                var previous=existingRows.SingleOrDefault(row=>row.Metric==metric);
                if(previous is not null)return previous;
                var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"{parameters.ParameterSetId:N}:observation:{metric}"));
                return new RegimeDiscoveryObservationMetricConfiguration
                {
                    RequirementId=new Guid(bytes.AsSpan(0,16)),Metric=metric,
                    Enabled=metric==ObservationMetricsType.VxTermStructure&&vxRows.Any(row=>row.Enabled),
                    MaximumAgeSeconds=metric==ObservationMetricsType.VxTermStructure
                        ?(vxRows.Length==0?345600:vxRows.Max(row=>row.MaximumAgeSeconds)):30,
                    PrepareAtStartup=metric==ObservationMetricsType.VxTermStructure&&
                        (vxRows.Length==0||vxRows.Any(row=>row.PrepareAtStartup)),
                    Monitor=metric==ObservationMetricsType.VxTermStructure&&
                        (vxRows.Length==0||vxRows.Any(row=>row.Monitor))
                };
            }).OrderBy(row=>row.Metric).ToArray();
    }
    static (SignalMetricsType Metric, int PeriodLength)? Definition(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema20Slope
            => (SignalMetricsType.Ema, 20),
        RegimeDiscoverySignalMetric.Ema50 or RegimeDiscoverySignalMetric.Ema50Slope
            => (SignalMetricsType.Ema, 50),
        RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema200Slope
            => (SignalMetricsType.Ema, 200),
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope
            => (SignalMetricsType.Rsi, 14),
        RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14
            or RegimeDiscoverySignalMetric.MinusDi14 => (SignalMetricsType.Adx, 14),
        RegimeDiscoverySignalMetric.MacdHistogram => (SignalMetricsType.Macd, 26),
        RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio
            => (SignalMetricsType.Atr, 14),
        RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio
            or RegimeDiscoverySignalMetric.BollingerPosition => (SignalMetricsType.BollingerBand, 20),
        RegimeDiscoverySignalMetric.Ema20Interaction or RegimeDiscoverySignalMetric.AtrNormalizedRange
            or RegimeDiscoverySignalMetric.RollingHigh20 or RegimeDiscoverySignalMetric.RollingLow20
            or RegimeDiscoverySignalMetric.BreakoutDistanceAtr => (SignalMetricsType.MarketStructure, 20),
        RegimeDiscoverySignalMetric.Tdi => (SignalMetricsType.Tdi, 13),
        _ => null
    };
    public ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion)
    {
        try
        {
            var canonical=ParameterCanonicalPayloadModel.Canonicalize(payloadJson);
            var structural=ParameterSchemaRegistry.Default.ValidateStructure(ComponentCode,schemaVersion,canonical);
            if(structural.Length!=0)return structural;
            var value=JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(canonical);
            if(value is null) return [new("PARAM.OBJECT_REQUIRED","Payload","A parameter object is required.")];
            var issues=new RegimeDiscoveryParameterSetValidationRules().Execute(value)
                .Select(x=>new ParameterValidationIssue("PARAM.VALIDATION_FAILED","Parameters",x.ErrorMessage)).ToList();
            if(value.SchemaVersion != schemaVersion) issues.Add(new("PARAM.SCHEMA_MISMATCH","SchemaVersion","Payload schema differs from selected schema."));
            if(issues.Count != 0 || value.SchemaVersion == 1) return issues.ToArray();
            if(value.SchemaVersion>=5)
            {
                var signalMetrics=value.SignalMetrics!;
                var observationMetrics=value.ObservationMetrics!;
                if(signalMetrics.Any(row=>row is null)||observationMetrics.Any(row=>row is null))
                    return [new("PARAM.VALIDATION_FAILED","Metrics","Null metric rows are invalid.")];
                if(signalMetrics.Any(row=>row.RequirementId==Guid.Empty)||signalMetrics.Select(row=>row.RequirementId).Distinct().Count()!=signalMetrics.Length)
                    issues.Add(new("PARAM.ROW_ID_INVALID","SignalMetrics","Signal row identities must be nonempty and unique."));
                if(observationMetrics.Any(row=>row.RequirementId==Guid.Empty)||observationMetrics.Select(row=>row.RequirementId).Distinct().Count()!=observationMetrics.Length)
                    issues.Add(new("PARAM.ROW_ID_INVALID","ObservationMetrics","Observation row identities must be nonempty and unique."));
                if(signalMetrics.Select(row=>(row.Metric,row.TimeFrame,row.PeriodLength)).Distinct().Count()!=signalMetrics.Length)
                    issues.Add(new("PARAM.DUPLICATE_SIGNAL","SignalMetrics","Metric/timeframe/period combinations must be unique."));
                if(observationMetrics.Select(row=>row.Metric).Distinct().Count()!=observationMetrics.Length)
                    issues.Add(new("PARAM.DUPLICATE_OBSERVATION","ObservationMetrics","Observation metrics must be unique."));
                foreach(var row in signalMetrics)
                {
                    var path=$"SignalMetrics[{row.RequirementId}]";
                    if(row.Metric==SignalMetricsType.Unknown||!Enum.IsDefined(row.Metric))issues.Add(new("PARAM.METRIC_INVALID",path,"Select a signal metric."));
                    if(row.TimeFrame==TimeFrameType.None||!Enum.IsDefined(row.TimeFrame))issues.Add(new("PARAM.TIMEFRAME_INVALID",path,"Select a signal timeframe."));
                    if(row.PeriodLength<=0)issues.Add(new("PARAM.PERIOD_INVALID",path,"Period length must be positive."));
                    if(row.MaximumAgeSeconds<=0)issues.Add(new("PARAM.AGE_INVALID",path,"Maximum age must be positive."));
                }
                foreach(var row in observationMetrics)
                {
                    var path=$"ObservationMetrics[{row.RequirementId}]";
                    if(row.Metric==ObservationMetricsType.Unknown||!Enum.IsDefined(row.Metric))issues.Add(new("PARAM.METRIC_INVALID",path,"Select an observation metric."));
                    if(row.MaximumAgeSeconds<=0)issues.Add(new("PARAM.AGE_INVALID",path,"Maximum age must be positive."));
                }
                try
                {
                    var projection=RegimeDiscoveryMetricConfigurationProjection.ToLegacyRequirements(
                        value.ParameterSetId,signalMetrics,observationMetrics,value.Horizon);
                    if(!projection.Select(row=>(row.Metric,row.TimeFrame,row.Enabled,row.MaximumAgeSeconds,row.PrepareAtStartup,row.Monitor))
                        .SequenceEqual(value.SignalRequirements!.Select(row=>(row.Metric,row.TimeFrame,row.Enabled,row.MaximumAgeSeconds,row.PrepareAtStartup,row.Monitor))))
                        issues.Add(new("PARAM.PROJECTION_MISMATCH","SignalRequirements","The evaluator projection does not match the normalized editor selections."));
                }
                catch(ArgumentException error)
                {
                    issues.Add(new("PARAM.METRIC_UNSUPPORTED","Metrics",error.Message));
                }
            }            var rows=value.SignalRequirements!;
            if(rows.Any(x=>x is null)) return [new("PARAM.VALIDATION_FAILED","SignalRequirements","Null rows are invalid.")];
            if(rows.Select(x=>x.RequirementId).Distinct().Count()!=rows.Length || rows.Any(x=>x.RequirementId==Guid.Empty))
                issues.Add(new("PARAM.ROW_ID_INVALID","SignalRequirements","Row identities must be nonempty and unique."));
            if(rows.Select(x=>(x.Metric,x.TimeFrame)).Distinct().Count()!=rows.Length)
                issues.Add(new("PARAM.DUPLICATE_SIGNAL","SignalRequirements","Metric/timeframe pairs must be unique."));
            var defaults=Defaults(value);
            foreach(var row in rows)
            {
                var path=$"SignalRequirements[{row.RequirementId}]";
                if(value.SchemaVersion>=3&&row.Enabled)
                {
                    try{SignalStartupPlanModel.Producer(row.Metric,row.TimeFrame);}
                    catch(ArgumentException error){issues.Add(new("SIGNAL.PRODUCER_UNSUPPORTED",path,error.Message));}
                }
                if(row.MaximumAgeSeconds<=0) issues.Add(new("PARAM.AGE_INVALID",path,"Maximum age must be positive."));
                if((value.SchemaVersion<5&&!defaults.Any(x=>x.Metric==row.Metric&&x.TimeFrame==row.TimeFrame)) || row.CalculationConfigurationId!=$"{row.Metric}.v1")
                    issues.Add(new("SIGNAL.CONFIGURATION_UNSUPPORTED",path,$"{row.Metric}/{row.TimeFrame}/{row.CalculationConfigurationId} is not supported by the current evaluator."));
            }
            foreach(var mandatory in defaults.Where(x=>x.IsRequired))
                if(!rows.Any(x=>x.Metric==mandatory.Metric&&x.TimeFrame==mandatory.TimeFrame&&x.Enabled&&x.IsRequired))
                    issues.Add(new("SIGNAL.DEPENDENCY_MISSING","SignalRequirements",$"{mandatory.Metric}/{mandatory.TimeFrame} must remain enabled and required by the current calculation."));
            return issues.ToArray();
        }
        catch(Exception ex) when(ex is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        { return [new("PARAM.VALIDATION_FAILED","Payload",ex.Message)]; }
    }
    static int Duration(TimeFrameType frame)=>frame switch {
        TimeFrameType.FifteenSeconds=>15,TimeFrameType.OneMinute=>60,TimeFrameType.FiveMinutes=>300,
        TimeFrameType.FifteenMinutes=>900,TimeFrameType.ThirtyMinutes=>1800,TimeFrameType.OneHour=>3600,
        TimeFrameType.FourHours=>14400,TimeFrameType.Daily=>86400,
        _=>throw new ArgumentException("Unsupported evidence timeframe.")};
}







