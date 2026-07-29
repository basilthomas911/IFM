using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

public record LossProbabilityDataModel(
    double Value, 
    decimal Threshold, 
    int ThresholdCount);

