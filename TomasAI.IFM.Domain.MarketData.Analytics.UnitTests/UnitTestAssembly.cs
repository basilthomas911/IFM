using Xunit;

// These tests exercise process-wide actor attachment registries and timer
// schedulers. Serial execution prevents one scenario from mutating another
// scenario's actor-centric runtime state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
