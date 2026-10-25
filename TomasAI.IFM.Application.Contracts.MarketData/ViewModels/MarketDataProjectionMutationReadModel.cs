namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct MarketDataProjectionMutationReadModel(
    Guid MutationId,
    DateTime StartedOn)
{
    public bool IsFailed => StartedOn == DateTime.UnixEpoch;
}
