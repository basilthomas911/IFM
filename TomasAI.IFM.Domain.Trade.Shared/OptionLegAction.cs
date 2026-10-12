using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1720", Justification = "Preserve the existing serialized contract name describing the model value type.")]
    public enum OptionLegAction
    {
        Short,
        Long
    }
}
