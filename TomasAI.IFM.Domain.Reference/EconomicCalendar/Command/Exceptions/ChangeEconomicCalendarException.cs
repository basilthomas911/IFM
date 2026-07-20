namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;

/// <summary>
/// Represents errors that occur when attempting to change the economic calendar configuration.
/// </summary>
/// <remarks>This exception is typically thrown when an operation related to modifying the economic calendar fails
/// due to invalid input, system constraints, or other application-specific issues. Use the exception message to obtain
/// details about the cause of the failure. This exception supports serialization for scenarios where exception details
/// need to be transmitted across application domains.</remarks>
public class ChangeEconomicCalendarException : ApplicationException
{
    public ChangeEconomicCalendarException(string errorMessage) : base(errorMessage)
    {
    }

    public ChangeEconomicCalendarException() : base()
    {
    }

    public ChangeEconomicCalendarException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
