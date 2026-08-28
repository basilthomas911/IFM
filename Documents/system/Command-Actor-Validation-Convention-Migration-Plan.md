# CommandActor Validation Convention Migration Plan

> Implementation status (2026-08-28): V-00 through V-11 complete; V-12 mechanical,
> unit, BDD, and build qualification complete with the integration test-harness gaps recorded in
> [Command-Actor-Validation-Convention-Migration-Evidence.md](Command-Actor-Validation-Convention-Migration-Evidence.md).

## 1. Objective

Migrate every domain `CommandActor` to the command-ingress validation convention defined in
`Actor-Implementation-Conventions.md`, using `FundCommandActor` as the executable reference.

The migration must guarantee that:

- every mutable domain flow enters through a validated command;
- parsing only materializes a supported command through the read-only `_parseMap`;
- every supported concrete command has exactly one entry in a read-only, exact-type
  `_validationMap`;
- validation visibly begins with `CommandId`, then validates `EntityId`, every non-header payload
  parameter, and any duplicated identity/cross-parameter invariant;
- ordinary invalid data is accumulated into one `List<ValidationError>` and thrown once as one
  `CommandValidationException`;
- queries, events, and realtime actors do not duplicate command-domain payload validation;
- state-dependent and external-authority checks remain outside the deterministic ingress map; and
- no stricter validation change can reach a gate without updated unit, BDD, and integration
  qualification.

This plan does not change command audit ordering, command deduplication semantics, event contracts,
or domain execution behavior. Those require separate design approval.

## 2. Baseline inventory

The current repository contains 37 domain command actors and 156 concrete command contracts.
`FundCommandActor`, covering eight commands, is the completed reference. The remaining migration is
36 actors and 148 command contracts.

| Domain group | Actors | Commands | Current disposition |
| --- | ---: | ---: | --- |
| Application | 1 | 2 | Migrate |
| Fund | 2 | 11 | `FundCommandActor` complete; Fund Transaction remains |
| Market Data core and Securities | 4 | 15 | Migrate; three actors mix external lookup with ingress validation |
| Market Data Feed | 6 | 22 | Migrate |
| Market Data Analytics | 14 | 30 | Migrate in two gates |
| Option Pricer | 2 | 7 | Migrate |
| Reference | 2 | 6 | Migrate |
| System Administration | 1 | 28 | Migrate dynamic Database Backup map in a dedicated gate |
| Trade | 5 | 35 | Migrate core actors, then workflow actors |
| **Total** | **37** | **156** | **36 actors / 148 commands remain** |

Observed legacy shapes that must be retired are:

- mutable `Dictionary<string, ...>` validation maps keyed by command type name;
- `Action<ICommand>` entries that throw inside the validator and cannot aggregate errors;
- validation delegates that receive `IReferenceLookupService` or another external dependency;
- actors with no visible `ValidateCommandId` or `EntityId` call;
- partial payload validation that validates only selected properties;
- domain-specific validation helpers in the global `ValidationErrorsExtension`; and
- the reflection-generated Database Backup validation map, which hides the validation contract for
  28 commands.

## 3. Required target shape

Every migrated actor must use:

```csharp
static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>>
    _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
{
    [typeof(SomeCommand)] = command =>
    {
        var typed = (SomeCommand)command;
        return new List<ValidationError>()
            .ValidateCommandId(typed.CommandId, typed.CommandName)
            .ValidateSomeEntityId(typed.EntityId, typed.CommandName)
            .ValidateSomePayload(typed.Payload)
            .ValidateEntityIdMatches(
                typed.EntityId,
                typed.Payload?.SomeId,
                nameof(typed.Payload),
                typed.CommandName);
    }
};
```

The supported concrete type sets in `_parseMap`, `_validationMap`, and `_receiveMap` must be equal.
Unsupported types fail closed. `OnValidateAsync` must perform only common argument checks, exact-type
map resolution, invocation, and the single aggregate throw.

The common serialized header remains keys 0 through 5: `CommandId`, `Subject`, `PostEvents`,
`EntityId`, `ErrorCode`, and `RouteTo`. `CommandId` and `EntityId` are explicitly validated;
`Subject` is checked by parsing/routing; the remaining technical fields are not treated as domain
payload values.

## 4. Validator ownership rules

For each command contract:

1. Inventory every serialized property after the common header.
2. Classify it as scalar/simple, structured payload, collection, duplicated identity, or
   state/external-authority input.
3. Put intrinsic `EntityId` validation immediately after its shared identifier definition.
4. Put structured read-model FluentValidation rules and their `List<ValidationError>` adapter after
   the shared read-model definition.
5. Put command-specific scalar, enum, collection, and cross-parameter extensions in the domain's
   `Command/Validation` folder.
6. Keep only universal checks such as `ValidateCommandId` and the aggregate throw in the global
   `ValidationErrorsExtension`.
7. Do not create a generic `PayloadId`. Validate identities actually present in the command and
   cross-check them only after their independent rules pass.
8. Treat null structured payloads and null collection elements as validation errors, never as
   `NullReferenceException`.
9. For arrays or batches, validate the collection itself and every element, preserving an index or
   stable item identity in each error message.
10. Do not guess optional-field semantics. Record whether null, empty, zero, or the enum's zero value
    is valid before implementing the rule.

External reference lookups and checks against loaded aggregate state are not deterministic payload
validation. Move them to the mapped command extension after state load, while preserving the rule
that failure prevents event creation and persistence.

## 5. Standard actor migration procedure

Apply these steps to every actor within a gate:

1. Record the actor's parse, validation, and receive command sets.
2. Record `EntityId` type and all concrete payload fields for every command.
3. Locate existing embedded validators and shared read-model rules before creating new ones.
4. Define missing intrinsic identifier and structured-payload rules in their owning shared files.
5. Move domain-only scalar and cross-parameter checks out of global validation extensions.
6. Replace string-keyed or `Action<ICommand>` validation with the exact-type read-only map.
7. Make every entry visibly start with `ValidateCommandId`, followed by `EntityId`.
8. Validate all payload parameters and add identity/cross-parameter comparisons.
9. Move external/state validation to the execution boundary where required.
10. Update the actor, payload-validator, BDD, and integration tests described below.
11. Run the gate's conformance test, focused domain suites, serialization tests, and build.
12. Do not begin the next gate until the current gate has no warnings, skipped required tests, or
    unexplained behavior changes.

## 6. Test requirements for every migrated actor

### 6.1 Unit tests

Unit coverage is exhaustive for validation rules and must include:

- parse/validation/receive map set parity;
- valid command acceptance for every concrete command type;
- direct-map rejection of an empty `CommandId`;
- invalid or null `EntityId`;
- every scalar boundary, invalid enum value, required string, date boundary, and collection rule;
- null structured payload and null batch element behavior;
- every property in each structured FluentValidation model;
- duplicated identity match and mismatch;
- multiple invalid values producing one aggregate `CommandValidationException` with the command's
  error code;
- unsupported concrete command type failure;
- validation failure preventing state load, execution, event application, and persistence;
- external/state-dependent checks occurring after state load and before event creation; and
- unchanged happy-path extension behavior.

Shared identifier and read-model validator tests belong with the project that owns the shared type.
Actor tests verify composition and dispatch, not duplicate every FluentValidation rule.

### 6.2 BDD tests

BDD coverage must express business-observable behavior:

- Given a valid command, the existing happy-path domain event/result remains unchanged.
- Given an invalid command payload, no domain event is applied and no workflow continuation is
  published.
- Given a mismatched `EntityId` and payload identity, the command fails before domain execution.
- Given several invalid payload fields, the returned failure contains the aggregate errors.
- Given a bulk command with an invalid item, the error identifies the item/index and the batch does
  not partially execute unless the existing domain contract explicitly defines partial execution.
- Given a valid payload but missing external/state authority, the business check fails at the
  execution boundary, not in `_validationMap`.

Existing BDD scenarios should be updated to construct fully valid commands before testing a later
business rule; otherwise stricter ingress validation can mask the behavior the scenario intends to
exercise.

### 6.3 Integration tests

Integration coverage is representative at the transport and persistence boundaries, while unit
tests remain exhaustive per field. Each actor requires:

- one valid serialized command transported through the real actor ingress path;
- one invalid non-empty-`CommandId` command returning failure with no domain event, state change,
  read-model mutation, or downstream terminal publication;
- one `EntityId`/payload mismatch transported through serialization;
- round-trip coverage for any newly validated identifier/read model;
- actual reference/state infrastructure coverage for actors whose lookup validation moves out of
  `_validationMap`;
- batch atomicity coverage for commands carrying arrays/collections; and
- regression coverage proving valid command persistence and projection remain unchanged.

The current base audit-before-domain-validation order is preserved: an invalid command with a valid
non-empty `CommandId` may have an audit reservation even though it creates no domain event. Tests
must not silently redefine that behavior.

## 7. Automated conformance requirements

Gate V-01 expands `scripts/Test-CommandActorConventions.ps1` and adds architecture tests that fail
when any domain actor violates the mechanical convention. The checks must verify:

- all 37 domain command actors are discovered intentionally;
- all parse maps are read-only and delegate to `ParseMappedCommand`;
- every validation map has the exact
  `IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>>` shape;
- validation maps do not use string keys, `Action<ICommand>`, or external-service delegate
  parameters;
- parse, validation, and receive supported concrete command sets are equal;
- every explicit validation entry contains the visible `ValidateCommandId` call;
- no domain actor performs local command auditing;
- no migrated actor uses a domain-specific helper from the global validation extension class; and
- the command actor template demonstrates the same parse, validation, and receive pattern.

Source checks provide fast feedback. Domain unit tests remain authoritative for semantic payload and
identity coverage.

## 8. Migration gates

### Gate V-00 — Freeze inventory and baseline

Deliverables:

- commit the actor/command inventory used by this plan;
- record current passing counts for all affected BDD, unit, and integration projects;
- identify environment-backed integration prerequisites and explicitly mark unavailable suites;
- produce a per-command worksheet containing header, `EntityId`, payload properties, existing
  validators, and state/external checks; and
- confirm `FundCommandActor` remains the reference implementation.

Exit gate: 37 actors and 156 concrete command contracts are accounted for with no unknown owner.

### Gate V-01 — Framework, template, and conformance foundation

Scope:

- update the command actor template to the Fund validation pattern;
- add the automated conformance checks in section 7;
- add reusable test helpers for map parity, aggregate exception assertions, and no-execution-on-
  validation-failure assertions; and
- document the temporary allow-list for actors not yet migrated so conformance tightens after every
  gate rather than remaining disabled until the end.

Exit gate: Fund passes the strengthened convention; every remaining legacy actor is explicitly
allow-listed and the list can only shrink.

### Gate V-02 — Application and Lookup Type

Actors and contracts:

- `ApplicationCommandActor` — 2 commands;
- `LookupTypeCommandActor` — 3 commands.

Test projects:

- Application Actor Unit/BDD/Integrated tests;
- Reference Unit/BDD/Integration tests.

Exit gate: 2 actors and 5 commands removed from the legacy allow-list.

### Gate V-03 — Fund Transaction

Actors and contracts:

- `FundTransactionCommandActor` — 3 commands;
- regression qualification of the 8-command `FundCommandActor` reference.

Focus:

- single versus batch transaction validation;
- fund/order/trade identity consistency;
- batch index-aware errors and atomic failure.

Test projects:

- Fund Unit, BDD, and Integration tests.

Exit gate: all Fund command actors follow the convention.

### Gate V-04 — Market Data core and Securities

Actors and contracts:

- `EconomicCalendarCommandActor` — 4 commands;
- `YieldCurveRateCommandActor` — 4 commands;
- `FuturesContractCommandActor` — 3 commands;
- `FuturesOptionContractCommandActor` — 4 commands.

Focus:

- import/batch collection validation;
- contract identifier and date/value rules;
- move `IReferenceLookupService` checks out of ingress maps for the Securities actors;
- co-locate shared market-data identifier/read-model validators before Feed and Analytics consume
  them.

Test projects:

- Market Data Unit and Integration tests;
- Market Data Securities Unit, BDD, and Integration tests.

Exit gate: 4 actors and 15 commands removed from the legacy allow-list.

### Gate V-05 — Market Data Feed

Actors and contracts:

- `MarketDataFeedCommandActor` — 9 commands;
- `FuturesBarDataCommandActor` — 4 commands;
- `FuturesClosingPriceCommandActor` — 1 command;
- `FuturesEodDataCommandActor` — 2 commands;
- `FuturesOptionTickDataCommandActor` — 3 commands;
- `FuturesTickDataCommandActor` — 3 commands.

Focus:

- stream/request identity and start/stop symmetry;
- array/tick/bar payload validation and batch atomicity;
- move Futures Option Tick external lookup checks to the execution boundary;
- preserve non-durable feed behavior while rejecting invalid durable commands.

Test projects:

- Market Data Feed Unit, BDD, and Integration tests.

Exit gate: 6 actors and 22 commands removed from the legacy allow-list.

### Gate V-06 — Analytics indicator actors

Actors and contracts:

- `FuturesAdxSignalCommandActor` — 4 commands;
- `FuturesAtrSignalCommandActor` — 4 commands;
- `FuturesMacdSignalCommandActor` — 4 commands;
- `FuturesRsiSignalCommandActor` — 4 commands.

Focus:

- start/stop/generate command parity;
- futures contract identity, timeframe, date, and calculation parameter validation;
- shared signal payload validators without duplication between the four actors.

Test projects:

- Market Data Analytics Unit, BDD, and Integration tests.

Exit gate: 4 actors and 16 commands removed from the legacy allow-list.

### Gate V-07 — Remaining Analytics actors

Actors and contracts:

- `FuturesBbSignalCommandActor` — 1 command;
- `FuturesEmaSignalCommandActor` — 1 command;
- `FuturesItiSignalCommandActor` — 3 commands;
- `FuturesTdiSignalCommandActor` — 1 command;
- `FuturesTradeSessionBarSignalCommandActor` — 1 command;
- `FuturesTradeSignalCommandActor` — 1 command;
- `FuturesVwapSignalCommandActor` — 2 commands;
- `FuturesVxTermStructureSignalCommandActor` — 1 command;
- `FuturesAnalyticsHistoricalDataLoaderCommandActor` — 1 command;
- `MarketOutlookSnapshotCommandActor` — 2 commands.

Focus:

- replace `Action<ICommand>` validators in publisher/loader/outlook actors;
- validate ITI and market-outlook structured payloads completely;
- keep realtime payload processing outside this command-only convention;
- preserve recovery-command and observation/publish semantics.

Test projects:

- Market Data Analytics Unit, BDD, and Integration tests.

Exit gate: 10 actors and 14 commands removed from the legacy allow-list; Analytics is complete.

### Gate V-08 — Option Pricer

Actors and contracts:

- `SpreadDistributionCommandActor` — 2 commands;
- `SpreadDistributionJobCommandActor` — 5 commands.

Focus:

- distribution identifiers, numeric ranges, arrays, and job lifecycle values;
- identity consistency between job commands and distribution payloads;
- keep loaded job-state transition rules in execution.

Test projects:

- Option Pricer Unit, BDD, and Integration tests.

Exit gate: 2 actors and 7 commands removed from the legacy allow-list.

### Gate V-09 — Trade core

Actors and contracts:

- `OptionTradeCommandActor` — 15 commands;
- `TradePlanCommandActor` — 1 command;
- `TradePlanForwardLossLimitCommandActor` — 2 commands.

Focus:

- option order/trade/leg identifiers and cross-identity validation;
- spread data and bulk delete/insert atomicity;
- replace Trade Plan `Action<ICommand>` validators;
- keep position/order lifecycle decisions dependent on loaded state in command extensions.

Test projects:

- Trade Unit, BDD, and Integrated tests.

Exit gate: 3 actors and 18 commands removed from the legacy allow-list.

### Gate V-10 — Intrinsic Time workflow and Regime Discovery configuration

Actors and contracts:

- `IntrinsicTimeStrategyWorkflowCommandActor` — 16 commands;
- `RegimeDiscoveryCommandActor` — 1 command;
- `RegimeDiscoveryConfigurationCommandActor` — 3 commands.

Focus:

- preserve immutable workflow-view and parameter-set version contracts;
- validate workflow, stage, execution, parameter-set, and correlation identities independently;
- cross-check repeated workflow/stage identifiers without inventing payload IDs;
- keep workflow state-machine transitions and parameter-set existence/version checks after state or
  configuration load;
- cover Execute/Complete/Fail/Timeout paths without permitting invalid input to advance a workflow.

Test projects:

- Trade Unit, BDD, and Integrated tests;
- Reference Unit, BDD, and Integration tests.

Exit gate: 3 actors and 20 commands removed from the legacy allow-list, with Regime Discovery and
strategy workflow fully qualified.

### Gate V-11 — System Administration Database Backup

Actor and contracts:

- `DatabaseBackupCommandActor` — 28 commands.

Focus:

- replace the reflection-generated `Dictionary<string, Action<ICommand>>` validation contract with
  an explicit exact-type read-only map;
- adapt `IDatabaseBackupValidatable` rules to append `ValidationError` values instead of throwing
  independently;
- cover request, approval, cancellation, policy, legal hold, retention, reconciliation, progress,
  verification, completion, and failure command families;
- preserve the separate public/internal command execution paths.

Test projects:

- System Administration Unit, BDD, and Integration tests.

Exit gate: the final actor and 28 commands are removed from the legacy allow-list.

### Gate V-12 — System-wide qualification and closeout

Deliverables:

- remove the temporary legacy allow-list;
- run the CommandActor convention script with all 37 actors enforced;
- run all affected unit and BDD suites;
- run all available domain integration/integrated suites and record any environment-blocked suite;
- run serialization/message-contract tests for every changed shared identifier/read model;
- build `TomasAI.IFM.sln` with zero warnings and zero errors;
- verify no domain-specific validator remains in global `ValidationErrorsExtension` unless separately
  approved as genuinely universal;
- update `Actor-Implementation-Conventions.md` with any approved exception discovered during the
  migration; and
- publish a final actor/command/test matrix showing 37/37 actors and 156/156 command contracts
  compliant.

Exit gate: no allow-list, no unsupported validation-map shape, no unqualified command contract, and
all available test suites green.

## 9. Gate evidence template

Every gate completion record must include:

| Evidence | Required result |
| --- | --- |
| Actor inventory | All scoped actors and command types listed |
| Map parity | Parse = validation = receive concrete type sets |
| Validator ownership | Identifier/read-model/domain-command rules located correctly |
| Unit tests | Exhaustive field and composition coverage; zero failures |
| BDD tests | Happy path and invalid-command business behavior; zero failures |
| Integration tests | Transport/persistence/reference cases; zero failures or explicit environment blocker |
| Conformance script | Scoped actors removed from allow-list; zero violations |
| Build | Zero warnings and zero errors |
| Scope review | No query/event/realtime validation duplication and no unrelated behavior change |

## 10. Known risks and controls

- **Previously accepted defaults become invalid.** Update factories and fixtures only when the new
  rule reflects approved domain semantics; do not weaken validation merely to preserve a fixture.
- **Shared validator movement breaks namespaces.** Move a shared type's rules and all consumers in
  one gate, then run serialization and downstream-domain builds.
- **External lookup movement changes ordering.** Add tests proving deterministic ingress validation
  happens before state load and authoritative lookup happens before event application.
- **Bulk commands partially validate.** Require element-by-element, index-aware errors and explicit
  atomicity tests.
- **Enum zero values are ambiguous.** Decide whether zero is a valid business value or sentinel for
  each enum before writing the rule.
- **Cross-identity checks create duplicate noise.** Run mismatch checks only after both identities
  independently pass intrinsic validation.
- **Large mechanical changes conceal omissions.** Migrate by gates, require map-set parity, and never
  mass-rewrite all 36 remaining actors without gate qualification.
- **Current workflow work overlaps Gate V-10.** Preserve the active worktree and migrate the workflow
  actors only after their current command contracts stabilize.

## 11. Completion definition

The migration is complete only when all 37 domain command actors and all 156 concrete command
contracts satisfy the convention, the temporary allow-list is empty, every command has documented
payload and identity validation, all available unit/BDD/integration suites pass, and the full
solution builds with zero warnings and zero errors.
