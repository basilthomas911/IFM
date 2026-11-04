using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Explicit owner-query evidence, fenced against intervening parameter or catalog mutations.</summary>
[MessagePackObject]
public sealed record ParameterCommandEvidence(
    [property: Key(0)] long RevisionFence,
    [property: Key(1)] ParameterSetVersion? ExactVersion = null,
    [property: Key(2)] ImmutableArray<ParameterAssignmentRevision> Assignments = default,
    [property: Key(3)] ImmutableArray<ParameterSetVersion> Versions = default,
    [property: Key(4)] bool HasActiveAssignments = false,
    [property: Key(5)] ParameterCatalogEvidence? Catalog = null,
    [property: Key(6)] ParameterLegacyVersion? LegacyVersion = null);

/// <summary>Identity and publication evidence for an exact immutable strategy-catalog version.</summary>
[MessagePackObject]
public sealed record ParameterCatalogEvidence(
    [property: Key(0)] Guid DefinitionId,
    [property: Key(1)] int Version,
    [property: Key(2)] string ContentHash,
    [property: Key(3)] bool Published,
    [property: Key(4)] bool SupportedBuilder);
