using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// Represents the default configuration for a futures contract, including currency, exchange, multiplier, security
/// types, and symbol information.
/// </summary>
/// <remarks>This view model is typically used to provide default values or templates for futures contract
/// definitions in trading or financial applications. All properties are initialized to empty strings by
/// default.</remarks>
[MessagePackObject(AllowPrivate = true)]
public class DefaultFuturesContractDefinitionsReadModel
{
    


    [Key(0)]
    public string Currency { get; set; }
    
    [Key(1)]
    public string Exchange { get; set; }
    
    [Key(2)]
    public string Multiplier { get; set; }
    
    [Key(3)]
    public string SecurityType { get; set; }
    
    [Key(4)]
    public string OptionSecurityType { get; set; }
    
    [Key(5)]
    public string Symbol { get; set; }
}
