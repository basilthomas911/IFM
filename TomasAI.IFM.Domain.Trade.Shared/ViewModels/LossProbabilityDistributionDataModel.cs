using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

public record LossProbabilityDistributionDataModel(
    double Mean,
    double StdDev);
