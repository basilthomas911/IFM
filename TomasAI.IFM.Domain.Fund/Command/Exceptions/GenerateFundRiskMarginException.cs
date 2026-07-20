namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents errors that occur during the generation of a fund risk margin.
/// </summary>
/// <remarks>This exception is typically thrown when an error condition is encountered while calculating or
/// generating a fund's risk margin. Use this exception to distinguish fund risk margin generation errors from other
/// application exceptions.</remarks>
public class GenerateFundRiskMarginException : ApplicationException
{
    public GenerateFundRiskMarginException(string errorMessage):base(errorMessage)
    {
    }

    public GenerateFundRiskMarginException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
