using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Application.CommandParameters;

/// <summary>
/// Represents the parameters required to shut down an application.
/// </summary>
/// <param name="ValueDate">The value (trading) date for which the application should be shut down.</param>
/// <param name="EntityId">The ID of the application entity to shut down.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record ShutdownApplicationParameter(DateOnly ValueDate, ApplicationEntityId EntityId, int ErrorCode)
    : ICommandParameter<ApplicationEntityId>;
