using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one accepted or proposed strategy workflow execution with a UUIDv7 value.</summary>
[MessagePackObject]
public readonly record struct StrategyWorkflowId
{
    /// <summary>Gets the underlying UUIDv7 value.</summary>
    [Key(0)]
    public Guid Value { get; init; }

    /// <summary>Initializes an execution identity from its underlying value.</summary>
    /// <param name="value">UUIDv7 execution identity.</param>
    [SerializationConstructor]
    public StrategyWorkflowId(Guid value) => Value = value;

    /// <summary>Creates a new time-ordered UUIDv7 workflow execution identity.</summary>
    /// <param name="timeProvider">Provides the UTC timestamp embedded in the UUIDv7 value.</param>
    /// <returns>A new workflow execution identity.</returns>
    public static StrategyWorkflowId New(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new(Guid.CreateVersion7(timeProvider.GetUtcNow()));
    }

    /// <summary>Parses a workflow execution identity.</summary>
    /// <param name="value">Text containing a GUID in any supported format.</param>
    /// <returns>The parsed workflow execution identity.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not a GUID.</exception>
    public static StrategyWorkflowId Parse(string value) => new(Guid.Parse(value));

    /// <summary>Attempts to parse a workflow execution identity.</summary>
    /// <param name="value">Text containing a GUID in any supported format.</param>
    /// <param name="workflowId">Receives the parsed identity when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out StrategyWorkflowId workflowId)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            workflowId = new StrategyWorkflowId(parsed);
            return true;
        }

        workflowId = default;
        return false;
    }

    /// <summary>Formats the UUID without separators.</summary>
    public override string ToString() => Value.ToString("N");
}
