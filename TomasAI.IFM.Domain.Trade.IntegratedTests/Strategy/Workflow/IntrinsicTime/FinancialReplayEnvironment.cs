namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;
/// <summary>Test-process settings are restored before the assembly-shared host's actors restart.</summary>
sealed class FinancialReplayEnvironment : IDisposable
{
    readonly Dictionary<string,string?> prior=[];
    public FinancialReplayEnvironment(IReadOnlyDictionary<string,string?> settings,string root)
    {foreach(var (key,value) in DomainActorIntegrationInfrastructureFixture.IsolatedRecoveryDirectories(root)){prior[key]=Environment.GetEnvironmentVariable(key);Environment.SetEnvironmentVariable(key,value);}
     foreach(var (key,value) in settings){prior[key]=Environment.GetEnvironmentVariable(key);Environment.SetEnvironmentVariable(key,value);}}
    public void Dispose(){foreach(var (key,value) in prior)Environment.SetEnvironmentVariable(key,value);}
}
