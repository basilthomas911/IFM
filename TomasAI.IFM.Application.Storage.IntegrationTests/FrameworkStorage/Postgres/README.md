# Framework.Storage PostgreSQL integration tests

These tests exercise the public `Framework.Storage` repository context against the PostgreSQL event-source schema.
They cover commands with zero, one, and multiple parameter values; queued transactional commands; mutable and
immutable results; single and scalar queries; map/reduce; ordinal mapping; cancellable async streaming with early
enumerator disposal; validation failures; explicit server-side preparation; single-round-trip `NpgsqlBatch` queue
execution; and transaction rollback.

Production PostgreSQL bindings are reflection-free. Each `IBindValue` returns an ordered `NpgsqlParameter[]` of
strongly typed, unnamed `NpgsqlParameter<T>` values that correspond directly to native `$1`, `$2`, ... SQL
placeholders. Three database-independent catalog tests validate every Event Source, Log, and Sequence ID binding's
parameter count, ordinal, value, explicit `NpgsqlDbType`, and generic parameter type.

The ordinal contract uses these existing tables:

- `event_stream_id`
- `event_name_id`
- `event_log`
- `command_log`
- `event_projector_state`

## Configuration

Set `IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION` to a credential-free PostgreSQL connection string whose database is
dedicated to integration tests. Set the runtime environment to `Test` and provide credentials through
`POSTGRES_TEST_KEY`. The fixture creates missing event-source schema objects, but it does not create the database.

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=event-source-test-db'
$env:POSTGRES_TEST_KEY = '{"userid":"...","password":"..."}'
```

Do not point this variable at a production database. The suite uses explicit negative event identifiers and reserved
stream names beginning with `__framework_storage_postgres_it__`. It clears those rows before and after every test and
verifies that cleanup completed. It does not consume the event-source sequences.

## Run

```powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
  --filter Category=PostgresIntegration
```

To run only the database-independent positional catalog contract:

```powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
  --filter FullyQualifiedName~PostgresPositionalParameterCatalogTests
```

The collection is non-parallel so deterministic test identifiers cannot overlap within a test process.
