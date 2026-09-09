using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public partial interface IConfigurationDbContext
{
    Task InsertRiskManagementDraftAsync(RiskParameterSet policy,string description,string createdBy,CancellationToken token=default);
}

public sealed partial class ConfigurationDbContext
{
    /// <summary>Creates a validated draft in the existing Risk policy table. Publication and deployment binding remain explicit.</summary>
    public async Task InsertRiskManagementDraftAsync(RiskParameterSet policy,string description,string createdBy,CancellationToken token=default)
    {
        policy.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        await dbFactory.ConfigurationDb.Use("RiskManagement.Insert",SelectionInsert.Replace("trade_selection_parameter_set","risk_management_parameter_set"))
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId,policy.Version,policy.SchemaVersion,0,
                policy.Serialize(),policy.Hash(),description,DateTime.UtcNow,createdBy)).ExecuteCommandAsync(token).ConfigureAwait(false);
    }
}
