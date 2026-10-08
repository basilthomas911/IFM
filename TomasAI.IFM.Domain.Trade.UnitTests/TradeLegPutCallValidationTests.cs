using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Validation;
namespace TomasAI.IFM.Domain.Trade.UnitTests;
public sealed class TradeLegPutCallValidationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, true)]
    [InlineData((byte)0, false)]
    [InlineData((byte)3, false)]
    public void Submission_accepts_call_and_put_encoding(byte? putCall, bool accepted)
    {
        var order = new TradeOrderDefinition { Components = [new() { Legs = [new() { PutCall = putCall }] }] };
        var errors = TradeOrderDefinitionValidation.Validate(order);
        errors.Any(error => error.Contains("PutCall")).Should().Be(!accepted);
    }
}
