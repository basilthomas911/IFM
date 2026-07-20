namespace TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb;

internal class PredictiveModelDbCql
{
    public const string CreateFuturesItiTrendClassDataTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_class_data (
        symbol text,
        valueDate date,
        timestamp timestamp,
        sequenceId int,
        trendClass boolean,
        trendDirection float,
        trendDirectionMode float,
        trendDelta float,
        futuresRSI float,
        PRIMARY KEY ((symbol, valueDate), sequenceId)
    ) WITH CLUSTERING ORDER BY (sequenceId ASC);
    """;

    public const string CreateFuturesItiTrendClassModelTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_class_model (
        symbol text,
        valueDate date,
        startDate date,
        endDate date,
        count int,
        maximum double,
        mean double,
        median double,
        minimum double,
        skewness double,
        stdDev double,
        variance double,
        accuracy double,
        areaUnderPrecisionRecallCurve double,
        areaUnderRocCurve double,
        entropy double,
        f1Score double,
        modelData blob,
        PRIMARY KEY (symbol, valueDate)
    );
    """;

    public const string CreateFuturesItiTrendDeltaDataTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_delta_data (
        symbol text,
        valueDate date,
        sequenceId int,
        timestamp timestamp,
        trendDelta float,
        trendDirection float,
        trendDirectionMode float,
        futuresPrice float,
        trendExtreme float,
        futuresRSI float,
        PRIMARY KEY ((symbol, valueDate), sequenceId)
    ) WITH CLUSTERING ORDER BY (sequenceId ASC);
    """;

    public const string CreateFuturesItiTrendDeltaModelTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_delta_model (
        symbol text,
        valueDate date,
        startDate date,
        endDate date,
        count int,
        maximum double,
        mean double,
        median double,
        minimum double,
        skewness double,
        stdDev double,
        variance double,
        meanAbsoluteError double,
        meanSquaredError double,
        rootMeanSquaredError double,
        lossFunction double,
        rSquared double,
        modelData blob,
        PRIMARY KEY (symbol, valueDate)
    ) WITH CLUSTERING ORDER BY (valueDate ASC);
    """;
}
