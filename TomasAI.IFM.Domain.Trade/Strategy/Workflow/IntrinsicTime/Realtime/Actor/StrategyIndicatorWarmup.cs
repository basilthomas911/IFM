namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

/// <summary>Publishes the trading date whose stored strategy indicators have finished restoring.</summary>
public sealed class StrategyIndicatorWarmup
{
    int completedDay;

    public bool IsReady(DateOnly date) => date != default && Volatile.Read(ref completedDay) == date.DayNumber;

    public void Complete(DateOnly date) => Volatile.Write(ref completedDay, date.DayNumber);

    public void Reset() => Volatile.Write(ref completedDay, 0);
}
