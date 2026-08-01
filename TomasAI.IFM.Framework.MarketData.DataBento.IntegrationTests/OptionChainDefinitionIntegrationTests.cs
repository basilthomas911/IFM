namespace TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests;

public sealed class OptionChainDefinitionIntegrationTests
{
    [Fact]
    public void InvalidChainSelectorsAreRejectedAfterConnectionWasVerified()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = LiveTestGate.CreateConnectedQueries();

        var crossDataset = Assert.Throws<ArgumentException>(() =>
            queries.GetChainDefinitions(new OptionChainDefinitionRequest
            {
                Dataset = "OPRA.PILLAR",
                Underlying = "ES",
                MaturityDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Rights = OptionRightSelection.Both
            }));
        Assert.Contains("does not match", crossDataset.Message);

        var invalidRights = Assert.Throws<ArgumentException>(() =>
            queries.GetChainDefinitions(new OptionChainDefinitionRequest
            {
                Dataset = "GLBX.MDP3",
                Underlying = "ES",
                MaturityDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Rights = OptionRightSelection.None
            }));
        Assert.Contains("Select Call, Put, or Both", invalidRights.Message);
    }
}
