namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Runs an externally staged Portfolio qualification only when all prerequisite environment values exist.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PortfolioEnvironmentFactAttribute : FactAttribute
{
    /// <summary>Creates an opt-in fact for the specified externally provisioned values.</summary>
    public PortfolioEnvironmentFactAttribute(params string[] environmentVariables)
    {
        var missing = environmentVariables
            .Where(static name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            .ToArray();
        if (missing.Length > 0)
            Skip = $"Requires externally orchestrated Portfolio qualification values: {string.Join(", ", missing)}.";
    }
}
