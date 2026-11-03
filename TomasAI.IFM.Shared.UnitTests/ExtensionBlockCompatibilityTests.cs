using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests;

public sealed class ExtensionBlockCompatibilityTests
{
    [Fact]
    public void Null_receiver_guard_retains_original_parameter_name_for_both_call_forms()
    {
        IEvent source = null!;
        Assert.Equal("@event", Assert.Throws<ArgumentNullException>(() => source.CheckForEmptyCommandId()).ParamName);
        Assert.Equal("@event", Assert.Throws<ArgumentNullException>(() => EventExtension.CheckForEmptyCommandId(source)).ParamName);
    }

    [Fact]
    public void Null_tolerant_extension_can_still_be_called_statically_with_named_receiver()
    {
        Assert.True(StringExtension.IsEmpty(s: null!));
        Assert.Equal("tradeOrder", StringExtension.ToCamelCase(text: "Trade Order"));
    }
}
