namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum FrameworkOrderType : byte { Unknown = 0, Market = 1, Limit = 2 }
