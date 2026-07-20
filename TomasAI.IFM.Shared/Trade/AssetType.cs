using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum AssetType
    {
        Futures,
        Equity,
        InterestRate,
        Commodity,
        Index,
        FX,
        Crypto
    }

    public static class AssetTypeExtensions
    {
        public static string ToStringFast(this AssetType value) => value switch
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
