using FluentAssertions;
using TomasAI.IFM.UI.Net.Services.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies transport-neutral UI operation outcomes.</summary>
public sealed class UiOperationResultTests
{
    /// <summary>Verifies a successful result carries its UI-owned value without an error.</summary>
    [Fact]
    public void Success_CarriesValueWithoutError()
    {
        var result = UiOperationResult<string>.Success("ready");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ready");
        result.Error.Should().BeNull();
    }

    /// <summary>Verifies a failed result carries a stable error without a value.</summary>
    [Fact]
    public void Failure_CarriesErrorWithoutValue()
    {
        var result = UiOperationResult<string>.Failure(42, "failed");

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be(new UiOperationError(42, "failed"));
    }
}
