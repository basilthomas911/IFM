using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging;

public interface ICommandServiceApi
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Task<ServiceResult<Guid>> PostCommandAsync(string uriPath, ICommand command);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Task<ServiceResult<Guid>> ExecuteCommandAsync(string uriPath, ICommand command);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    Task<ServiceResult<Guid>> ExecuteCommandAsync(string uriPath, ICommandParameter command);

}
