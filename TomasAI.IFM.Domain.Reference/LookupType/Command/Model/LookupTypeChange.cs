using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Model;
/// <summary>Immutable proposed lookup definition and business rejection.</summary>
internal sealed record LookupTypeChange(LookupTypeReadModel? LookupType, string? RejectionReason = null);
