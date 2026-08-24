using TomasAI.IFM.UI.Net.Models.Reference;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference
{
    /// <summary>Formats an MDI forward-loss ratio for the status console.</summary>
    public class MDIForwardLossRatioUIViewModel
    {
        /// <summary>Creates display values from a UI-owned ratio model.</summary>
        public MDIForwardLossRatioUIViewModel(MdiForwardLossRatioUiModel e)
        {
            MDI = $"MDI >= {e.Mdi}";
            TrendDirection = $"{e.TrendDirection}";
            TradeType = $"{e.TradeType}";
            ForwardLossRatio = $"{e.ForwardLossRatio:F2}";
        }

        /// <summary>Gets the formatted MDI bucket.</summary>
        public string MDI { get; private set; }
        /// <summary>Gets the formatted trend direction.</summary>
        public string TrendDirection { get; private set; }
        /// <summary>Gets the formatted trade type.</summary>
        public string TradeType { get; private set; }
        /// <summary>Gets the formatted forward-loss ratio.</summary>
        public string ForwardLossRatio { get; private set; }

    }
}
