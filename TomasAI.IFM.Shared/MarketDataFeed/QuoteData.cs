using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Represents a single futures option quote event at a specific time and book level,
/// including operation type, side, price and size.
/// </summary>
/// <remarks>
/// MessagePack serializable. Follows the same serialization pattern used by other view models
/// (e.g., FundOrderReadModel). Operation: 0 = insert, 1 = update, 2 = delete.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record QuoteData
{
    /// <summary>Timestamp when the quote event occurred.</summary>
    [Key(0)]
    public DateTime QuoteTime { get; init; }

    /// <summary>Quote depth level classification (e.g., L1 or L2).</summary>
    [Key(1)]
    public QuoteLevelType LevelType { get; init; }

    /// <summary>Row index/position in the order book for the given level.</summary>
    [Key(2)]
    public int Position { get; init; }

    /// <summary>Operation applied to the row: 0 = insert, 1 = update, 2 = delete.</summary>
    [Key(3)]
    public int Operation { get; init; }

    /// <summary>Side of the quote: Ask or Bid.</summary>
    [Key(4)]
    public QuoteSide Side { get; init; }

    /// <summary>Type of quote update (e.g., Price or Size).</summary>
    [Key(5)]
    public QuoteType QuoteType { get; init; }

    /// <summary>Quoted price.</summary>
    [Key(6)]
    public double Price { get; init; }

    /// <summary>Quoted size (quantity).</summary>
    [Key(7)]
    public int Size { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public QuoteData() { }

    /// <summary>
    /// Initializes a new quote event with all serialized properties.
    /// </summary>
    /// <param name="quoteTime">Timestamp of the quote.</param>
    /// <param name="levelType">Quote depth level classification.</param>
    /// <param name="position">Row index in the order book.</param>
    /// <param name="operation">Operation: 0 = insert, 1 = update, 2 = delete.</param>
    /// <param name="side">Quote side.</param>
    /// <param name="quoteType">Quote update type.</param>
    /// <param name="price">Quoted price.</param>
    /// <param name="size">Quoted size.</param>
    public QuoteData(
        DateTime quoteTime,
        QuoteLevelType levelType,
        int position,
        int operation,
        QuoteSide side,
        QuoteType quoteType,
        double price,
        int size)
    {
        QuoteTime = quoteTime;
        LevelType = levelType;
        Position = position;
        Operation = operation;
        Side = side;
        QuoteType = quoteType;
        Price = price;
        Size = size;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Provides validation rules for <see cref="QuoteData"/> instances.
/// </summary>
public class QuoteDataValidationRules : BaseValidationRules, IValidationRules<QuoteData>
{
    private const string InstanceErrorMessage = "QuoteData instance cannot be null";

    /// <summary>
    /// Executes validation rules against the specified QuoteData instance.
    /// </summary>
    /// <param name="quoteData">The quote data to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(QuoteData quoteData)
        => Validate(quoteData, new QuoteDataValidator());

    /// <summary>
    /// Internal FluentValidation validator for QuoteData.
    /// </summary>
    class QuoteDataValidator : AbstractValidator<QuoteData>
    {
        public QuoteDataValidator()
        {
            RuleFor(x => x.QuoteTime)
                .Must(qt => qt != DateTime.MinValue && qt != DateTime.MaxValue)
                .WithMessage("QuoteData.QuoteTime must be a valid date and time");

            RuleFor(x => x.LevelType)
                .IsInEnum()
                .WithMessage("QuoteData.LevelType must be a valid QuoteLevelType");

            RuleFor(x => x.Position)
                .GreaterThanOrEqualTo(0)
                .WithMessage("QuoteData.Position must be >= 0");

            RuleFor(x => x.Operation)
                .InclusiveBetween(0, 2)
                .WithMessage("QuoteData.Operation must be 0 (insert), 1 (update), or 2 (delete)");

            RuleFor(x => x.Side)
                .IsInEnum()
                .WithMessage("QuoteData.Side must be a valid QuoteSide");

            RuleFor(x => x.QuoteType)
                .IsInEnum()
                .WithMessage("QuoteData.QuoteType must be a valid QuoteType");

            RuleFor(x => x.Price)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage("QuoteData.Price must be a valid non-negative number");

            RuleFor(x => x.Size)
                .GreaterThanOrEqualTo(0)
                .WithMessage("QuoteData.Size must be >= 0");
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<QuoteData> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("QuoteData", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}