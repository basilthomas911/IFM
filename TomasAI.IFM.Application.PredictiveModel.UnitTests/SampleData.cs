using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;

namespace TomasAI.IFM.Application.PredictiveModel.UnitTests;

public static class SampleData
{
    public static FuturesItiTrendModelBuildStartedEvent FuturesItiTrendModelBuildStartedEvent = new()
    {
        EntityId = new FuturesItiTrendEntityId("ES", new DateOnly(2023, 11, 16)),
        StartDate = new DateOnly(2023, 11, 16),
        EndDate = new DateOnly(2023, 11, 17),
        StartedBy = "basilt",
        StartedOn = new DateTime(2023, 11, 16)
    };

    public static FuturesItiTrendDeltaModelDataLoadedEvent FuturesItiTrendModelDataLoadedEvent = new ()
    {
        EntityId = new FuturesItiTrendEntityId("ES", new DateOnly(2023,11,16)),
        StartDate = new DateOnly(2023, 11, 16),
        EndDate = new DateOnly(2023, 11, 17),
        Statistics = new FuturesItiTrendModelDataStatistics(),
        LoadedBy = "basilt",
        LoadedOn = new DateTime(2023, 11, 16)
    };

    public static FuturesItiTrendDeltaModelTrainedEvent FuturesItiTrendDeltaModelTrainedEvent = new ()
    {
        EntityId = new FuturesItiTrendEntityId("ES", new DateOnly(2024, 01, 02)),
        StartDate = new DateOnly(2023, 12, 1),
        EndDate = new DateOnly(2024, 01, 02),
        Statistics = new FuturesItiTrendModelDataStatistics(),
        TrainedBy = "basilt",
        TrainedOn = new DateTime(2024, 01, 02),
    };

    public static FuturesItiTrendClassModelTrainedEvent FuturesItiTrendClassModelTrainedEvent = new ()
    {
        EntityId = new FuturesItiTrendEntityId("ES", new DateOnly(2024, 01, 02)),
        StartDate = new DateOnly(2023, 12, 1),
        EndDate = new DateOnly(2024, 01, 02),
        Statistics = new FuturesItiTrendModelDataStatistics(),
        TrainedBy = "basilt",
        TrainedOn = new DateTime(2024, 01, 02),
    };

}
