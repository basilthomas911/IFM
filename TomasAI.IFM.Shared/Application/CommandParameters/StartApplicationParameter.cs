using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Application.CommandParameters;

/// <summary>
/// Represents the parameters required to start an application.
/// </summary>
/// <param name="ValueDate">The value (trading) date for which the application should be started.</param>
/// <param name="EntityId">The entity ID associated with the application.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartApplicationParameter(DateOnly ValueDate, ApplicationEntityId EntityId, int ErrorCode)
    : ICommandParameter<ApplicationEntityId>;
