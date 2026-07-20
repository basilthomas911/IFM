using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

public record LossProbabilityDataModel(
    double Value, 
    decimal Threshold, 
    int ThresholdCount);

