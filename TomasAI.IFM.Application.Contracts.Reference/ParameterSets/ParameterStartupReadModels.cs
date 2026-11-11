namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>A repeatable-read view of a startup run and its projected report.</summary>
/// <param name="Run">The projected active run, or null.</param>
/// <param name="Report">The projected signal qualification report, or null.</param>
public sealed record ParameterStartupReadModel(ParameterStartupRun? Run, ParameterSignalStartupReport? Report);

/// <summary>Projected assignments and their exact versions selected in one database snapshot.</summary>
/// <param name="Assignments">The current assignment revisions.</param>
/// <param name="Versions">Their exact immutable parameter versions.</param>
public sealed record ParameterStartupInputsReadModel(ParameterAssignmentRevision[] Assignments, ParameterSetVersion[] Versions);
