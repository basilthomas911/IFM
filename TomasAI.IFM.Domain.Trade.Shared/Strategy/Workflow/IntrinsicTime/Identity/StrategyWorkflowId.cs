using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

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

/// <summary>Validates that a strategy workflow execution identity is a non-empty UUIDv7 value.</summary>
public sealed class StrategyWorkflowIdValidationRules
    : BaseValidationRules, IValidationStructRules<StrategyWorkflowId>
{
    /// <summary>Error returned for an empty execution identity.</summary>
    public const string EmptyErrorMessage = "StrategyWorkflowId: Value is required";

    /// <summary>Error returned for a GUID that is not version 7.</summary>
    public const string VersionErrorMessage = "StrategyWorkflowId: Value must be UUIDv7";

    static readonly Validator Rules = new();

    /// <summary>Validates the supplied workflow execution identity.</summary>
    /// <param name="workflowId">Execution identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(StrategyWorkflowId workflowId) => Validate(workflowId, Rules);

    sealed class Validator : AbstractValidator<StrategyWorkflowId>
    {
        public Validator()
        {
            RuleFor(x => x.Value)
                .NotEmpty()
                .WithMessage(EmptyErrorMessage);
            RuleFor(x => x.Value)
                .Must(static value => value == Guid.Empty || value.Version == 7)
                .WithMessage(VersionErrorMessage);
        }
    }
}
