namespace TomasAI.IFM.Shared.Application;

public static class OptionPricerUriPath
{
    public const string InsertSpreadDistribution = "/api/optionpricer/spreaddistribution/insert";
    public const string DeleteSpreadDistribution = "/api/optionpricer/spreaddistribution/delete";
    public const string SubmitSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/submit";
    public const string CompleteSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/complete";
    public const string FailSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/failed";
    public const string ClearSpreadDistributionJob = "/api/optionpricer/spreaddistribution/job/clear";
    public const string DeleteSpreadDistributionJobsInProgress = "/api/optionpricer/spreaddistribution/job/inprogress/delete";
}
