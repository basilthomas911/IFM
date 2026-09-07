namespace TomasAI.IFM.Framework.Storage;

/// <summary>One database page; the opaque driver state resumes the same statement.</summary>
public sealed record QueryPage<T>(T[] Items, byte[]? PagingState);
