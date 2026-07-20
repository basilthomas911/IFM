using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

public record IronCondorForwardDeltaDataModel(
   double ForwardDeltaValue)
{
    public double Min => Math.Round(ForwardDeltaValue, 3);
    public double Value => Math.Round(ForwardDeltaValue + 0.004, 3);
    public double Max => Math.Round((ForwardDeltaValue) + 0.008, 3);
}
