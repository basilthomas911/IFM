using System.Diagnostics.Metrics;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Bounded operational measurements; no identifiers, SQL, principals or financial payloads are metric labels.</summary>
public static class FinancialTelemetry
{
    public const string MeterName="TomasAI.IFM.PortfolioFinancial";
    static readonly Meter Meter=new(MeterName,"1.0");
    static readonly Histogram<double> Transactions=Meter.CreateHistogram<double>("financial.transaction.duration","ms");
    static readonly Histogram<double> Locks=Meter.CreateHistogram<double>("financial.authority.lock.duration","ms");
    static readonly Counter<long> Retries=Meter.CreateCounter<long>("financial.transaction.rollback_retries");
    static readonly Counter<long> Outcomes=Meter.CreateCounter<long>("financial.transaction.outcomes");
    public static void Transaction(double milliseconds,string outcome)
    {
        // A diagnostic listener cannot change a confirmed financial outcome.
        try { var tag=new KeyValuePair<string,object?>("outcome",outcome);Transactions.Record(milliseconds,tag);Outcomes.Add(1,tag); } catch { }
    }
    public static void Lock(double milliseconds) { try { Locks.Record(milliseconds); } catch { } }
    public static void Retry() { try { Retries.Add(1); } catch { } }
}
