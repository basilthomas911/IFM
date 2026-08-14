using Xunit;

// This file is linked into every *IntegrationTests and *IntegratedTests project
// by Directory.Build.props. Keep the rule in compiled test assemblies so it is
// honored by IDE, command-line, and CI xUnit runners.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
