namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoRecoveryAttemptExecutor
{
    FeedRecoveryResult StopDisposeRecreateAndStart(FeedRecoveryAttempt attempt);
}
