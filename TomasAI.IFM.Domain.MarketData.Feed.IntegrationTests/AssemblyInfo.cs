using Xunit;

// These tests share fixed Scylla rows, NATS subjects, and a hosted application.
// Parallel classes can overwrite another test's arrange data before it is asserted.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
