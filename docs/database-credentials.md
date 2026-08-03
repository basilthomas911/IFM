# Database credentials

PostgreSQL and ScyllaDB connection strings throughout solution source/configuration contain only endpoint, database
or keyspace, and non-secret provider settings. `Framework.Storage` reads credentials from a provider- and
environment-specific environment variable immediately before creating a connection.

PostgreSQL resolves the base string with `NpgsqlConnectionStringBuilder`, injects `Username` and `Password`, and then
constructs `NpgsqlConnection`. ScyllaDB parses its base string with `CassandraConnectionStringBuilder` and passes the
credentials to `Cluster.Builder().WithCredentials(...)`. `Application.Storage` stores and forwards only the
credential-free base string.

This resolver currently applies only to PostgreSQL and ScyllaDB. SQL Server provider behavior and configuration are
unchanged.

## Runtime environment

The credential resolver reads `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT`.

- If neither variable is set, the database environment defaults to `Development`.
- If both variables are set, they must resolve to the same environment.
- Supported values and aliases are `Development`/`Dev`, `Test`/`Testing`, `Staging`/`Stage`, and
  `Production`/`Prod`.
- An unsupported environment or conflicting variables stops connection creation.
- Parsed credentials are cached by variable name and exact JSON value. The resolver reads the selected variable on
  each connection creation and refreshes the cached value when its JSON changes.

## Credential variables

| Runtime environment | PostgreSQL | ScyllaDB |
|---|---|---|
| Development | `POSTGRES_DEV_KEY` | `SCYLLADB_DEV_KEY` |
| Test | `POSTGRES_TEST_KEY` | `SCYLLADB_TEST_KEY` |
| Staging | `POSTGRES_STAGING_KEY` | `SCYLLADB_STAGING_KEY` |
| Production | `POSTGRES_PROD_KEY` | `SCYLLADB_PROD_KEY` |

Each variable contains JSON with two required, non-empty string properties:

```json
{"userid":"database-user","password":"database-password"}
```

Property names are case-insensitive. The resolver never includes the JSON value or password in its validation
errors. A connection string that still contains an inline username or password is rejected.

Credential resolution and connection creation fail before any database operation when the environment is invalid,
the selected key is missing/malformed, a required property is blank, or the base string contains inline credentials.
Validation messages identify the provider/key problem without including the JSON or password.

## Provider integration tests

The isolated Framework Storage provider suites use separate environment variables for their credential-free targets:

| Provider suite | Target connection variable | Credential variable in Test |
|---|---|---|
| PostgreSQL event-source schema | `IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION` | `POSTGRES_TEST_KEY` |
| ScyllaDB Fund schema | `IFM_SCYLLA_TEST_CONNECTION` | `SCYLLADB_TEST_KEY` |

Set `DOTNET_ENVIRONMENT=Test` (or set both application environment variables consistently) before running these
suites. The fixtures may create missing schema objects but never create the database/keyspace, and their configured
targets must be dedicated to integration testing.

## Examples

Credential-free PostgreSQL connection string:

```text
Host=localhost;Port=5432;Database=event-source-test-db
```

Credential-free ScyllaDB connection string:

```text
Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db
```

PowerShell test environment setup:

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=event-source-test-db'
$env:IFM_SCYLLA_TEST_CONNECTION = 'Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db'
$env:POSTGRES_TEST_KEY = '{"userid":"...","password":"..."}'
$env:SCYLLADB_TEST_KEY = '{"userid":"...","password":"..."}'
```

Do not commit real credential values, `.env` files, exported shell profiles, or generated deployment manifests.
Credentials that were previously committed must be rotated because removing them from the current files does not
remove them from Git history.
