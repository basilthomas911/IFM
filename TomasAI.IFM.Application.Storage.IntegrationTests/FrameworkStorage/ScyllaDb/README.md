# Framework.Storage ScyllaDB integration tests

These tests exercise the public `Framework.Storage` repository context against the four Fund tables in a real
ScyllaDB keyspace. They cover simple and prepared commands, multi-value command batches, sequential and logged
queued commands, mutable and pooled immutable result materialization, single and scalar queries, map/reduce, and
ordinal mapping for the Fund data types.

## Configuration

Set `IFM_SCYLLA_TEST_CONNECTION` to a credential-free ScyllaDB connection string whose default keyspace is dedicated
to tests. Set the runtime environment to `Test` and provide credentials through `SCYLLADB_TEST_KEY`. The fixture
creates missing Fund tables, but it does not create the keyspace.

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_SCYLLA_TEST_CONNECTION = 'Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db'
$env:SCYLLADB_TEST_KEY = '{"userid":"...","password":"..."}'
```

Do not point this variable at a production keyspace. The suite reserves Fund IDs `-1999999999` through
`-1999999984`, clears those partitions before and after each test, and verifies that cleanup completed.

## Run

```powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
  --filter Category=ScyllaDBIntegration
```

The collection is non-parallel so deterministic test partitions cannot overlap within a test process.
