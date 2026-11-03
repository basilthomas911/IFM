using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class AssetTypeExtensions
    {

        extension(AssetType value)
        {
            public string ToStringFast() => value switch
            {
                AssetType.Futures => nameof(AssetType.Futures),
                AssetType.Equity => nameof(AssetType.Equity),
                AssetType.InterestRate => nameof(AssetType.InterestRate),
                AssetType.Commodity => nameof(AssetType.Commodity),
                AssetType.Index => nameof(AssetType.Index),
                AssetType.FX => nameof(AssetType.FX),
                AssetType.Crypto => nameof(AssetType.Crypto),
                _ => value.ToString()
            };
        }
    }
}
