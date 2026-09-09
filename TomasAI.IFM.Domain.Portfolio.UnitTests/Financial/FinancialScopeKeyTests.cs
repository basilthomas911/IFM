using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

[Trait("Category","PortfolioFinancial")]
public sealed class FinancialScopeKeyTests
{
    [Fact]
    public void Product_scope_is_culture_independent_and_does_not_include_contract_expiry_or_timeframe()
    {
        FinancialScopeKeys.Underlying(" es ","glbx","usd").Should().Be("U1:ES|GLBX|USD");
        FinancialScopeKeys.Underlying("ES","GLBX","USD").Should().NotBe(FinancialScopeKeys.Underlying("ES","CME","USD"));
        FinancialScopeKeys.Underlying("ES","GLBX","USD").Should().NotBe(FinancialScopeKeys.Underlying("ES","GLBX","CAD"));
    }
    [Fact]
    public void Separators_cannot_alias_a_different_product_scope()
    {
        FinancialScopeKeys.Underlying("A|B","C","USD").Should().NotBe(FinancialScopeKeys.Underlying("A","B|C","USD"));
        Action missing=()=>FinancialScopeKeys.Underlying("ES","","USD");missing.Should().Throw<ArgumentException>();
    }
}
