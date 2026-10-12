namespace TomasAI.IFM.Framework.Storage;

/// <summary>One database page; the opaque driver state resumes the same statement.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
public sealed record QueryPage<T>(T[] Items, byte[]? PagingState);
