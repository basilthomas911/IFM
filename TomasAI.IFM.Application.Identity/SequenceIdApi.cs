using TomasAI.IFM.Shared.Identity;
namespace TomasAI.IFM.Application.Identity;
/// <summary>Application capability for allocating durable business identities.</summary>
public interface ISequenceIdApi
{
    ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default);
    ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default);
}
/// <summary>Preserves the provider's reservation and high-watermark semantics.</summary>
public sealed class SequenceIdApi(TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator generator) : ISequenceIdApi
{
    readonly TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator generator = generator ?? throw new ArgumentNullException(nameof(generator));
    public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken cancellationToken = default)
        => generator.GetSequenceIdAsync(name, cancellationToken);
    public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken cancellationToken = default)
        => generator.GetHighWatermarkAsync(name, cancellationToken);
}
