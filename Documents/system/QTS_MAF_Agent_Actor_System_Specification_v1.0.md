# QTS MAF Agent Actor System Specification v1.0

**Status:** Initial approved architecture baseline\
**Target:** QTS event-sourced actor system\
**Agent harness:** Microsoft Agent Framework (MAF)\
**Initial model:** Local Gemma 4\
**Core pattern:** QTS-owned `AgentSkill` inspired by `SKILL.md`,
parameterized invocation, disposable/stateless MAF execution.

## 1. Purpose

Integrate LLM agents without making deterministic QTS depend on MAF, a
particular model, an in-memory agent session, or synchronous LLM
availability.

**Primary invariant:** QTS actors own state, lifecycle, context,
authority and message semantics. MAF owns transient execution/tool
calling. The LLM supplies reasoning/generation only.

## 2. Mandatory architectural invariants

1.  QTS Actor System is always authoritative.
2.  All MAF/LLM work is asynchronous from QTS.
3.  No deterministic trading actor waits for an LLM result.
4.  MAF is disposable/stateless from the QTS durability perspective.
5.  Agent durable state and durable context are event sourced by QTS.
6.  LLM context is a temporary policy-controlled projection of durable
    state.
7.  Agent actors have minimal/no direct knowledge of MAF APIs.
8.  MAF interacts with QTS through allow-listed Command, Query and Event
    tools.
9.  Internal QTS tools are direct MAF function tools, not MCP.
10. MCP is reserved primarily for external advisory capabilities.
11. Skill definitions are QTS-owned and framework/model neutral.
12. LLM failure, timeout, expiry or outage cannot block trading.
13. Production trading never requires LLM availability.
14. Advice cannot override deterministic risk/execution authority unless
    a future bounded deterministic policy explicitly allows it.

## 3. Backbone architecture

``` mermaid
flowchart TD
 A[AgentActor] --> S[AgentSkill]
 A --> C[Event-Sourced Durable Context]
 S --> I[AgentSkillInvocation]
 C --> B[AgentContextBuilder]
 B --> I
 I --> Q[Durable Agent Work Queue]
 Q --> X[IAgentSkillExecutor]
 X --> M[MAF Adapter]
 M --> L[Local LLM]
 M --> G[QTS Agent Tool Gateway]
 G --> CT[Command Tools]
 G --> QT[Query Tools]
 G --> ET[Event Tools]
 CT --> R[QTS Actor Runtime]
 QT --> R
 ET --> R
```

## 4. Core components

### AgentActor

Owns durable identity, event-sourced state, skill identity/version,
durable context, invocation lifecycle, business specialization and
actor-side fallback policies. It does not own MAF agents, MAF sessions,
ChatClient details or model transport.

### AgentSkill

Stable capability definition: identity, instructions, context policy,
allowed tools, resources, execution policy and output policy.

### AgentContext

Authoritative event-sourced agent memory plus deterministic selection
rules for each invocation.

### AgentSkillInvocation

One parameterized execution of one skill.

### AgentToolGateway

Translates allow-listed MAF function calls into normal QTS Commands,
Queries and Events.

### IAgentSkillExecutor

Framework-neutral application boundary. `MafAgentSkillExecutor` is the
initial implementation.

## 5. `AgentSkill.md` model

Use `SKILL.md` as the design inspiration, but QTS owns the typed
contract. A physical Markdown representation may be generated/loaded
where useful.

A skill behaves like a parameterized stored procedure:

``` text
Stable AgentSkill definition
        +
Invocation parameters
        +
Selected durable context
        ↓
MAF / LLM execution
```

Example conceptual skill:

``` markdown
---
name: strategy-workflow-summary
version: 1.0
---

# Purpose
Interpret one finalized QTS strategy workflow.

# Rules
- Use only supplied QTS context.
- Never invent missing pipeline results.
- Distinguish business denial from infrastructure failure.
- Never bypass PortfolioRisk.
- Never initiate a trade.
- Produce 300–500 words.

# Allowed Events
- StrategyWorkflowSummarizedCompleteEvent
- StrategyWorkflowSummarizedFailEvent
```

Do not rewrite the skill for every invocation.

## 6. Parameterized invocation

Conceptual contract:

``` csharp
public sealed record AgentSkillInvocation
{
    public required Guid InvocationId { get; init; }
    public required string AgentId { get; init; }
    public required string SkillId { get; init; }
    public required string SkillVersion { get; init; }
    public required object Parameters { get; init; }
    public required AgentInvocationContext Context { get; init; }
    public required DateTimeOffset RequestedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public int Priority { get; init; }
    public string? CorrelationId { get; init; }
    public string? TraceId { get; init; }
}
```

The skill remains stable; parameters/context vary per execution.

## 7. Fully asynchronous execution

``` mermaid
sequenceDiagram
 participant A as QTS Actor
 participant E as Event Store
 participant P as Agent Projection
 participant Q as Durable Agent Queue
 participant M as MAF Worker
 participant L as LLM
 participant T as QTS Tool Gateway

 A->>E: Persist Agent Work Requested Event
 E-->>A: Commit
 Note over A: Actor continues immediately
 E->>P: Projection
 P->>Q: AgentSkillInvocation
 Q->>M: Async execution
 M->>L: Execute skill
 L->>M: Tool request
 M->>T: Command / Query / Event tool
 T->>A: Normal QTS message
```

**Asymmetry:** MAF may wait for QTS Query results. QTS never waits for
MAF.

A durable queue should survive QTS restart, MAF restart and LLM outage.
JetStream is a suitable implementation candidate, but application
contracts must remain JetStream-neutral.

Suggested invocation states: `Requested`, `Queued`, `Processing`,
`Completed`, `Failed`, `Unavailable`, `Expired`, `Cancelled`.

## 8. Durable context architecture

Hard rule:

> Event-sourced actor state is authoritative memory. LLM context is a
> temporary, policy-controlled projection of that memory for one
> invocation.

``` text
Event-Sourced Durable Agent State
        ↓
AgentContextPolicy
        ↓
AgentContextBuilder
        ↓
Immutable Invocation Context
        ↓
MAF / LLM
```

MAF conversation/session memory is never authoritative.

Durable context should be typed, e.g. `WorkflowResult`, `AdvisorResult`,
`PositionSummary`, `StrategyObservation`, `ResearchFinding`,
`ConfigurationReference`, `SystemInstruction`, `ResourceReference`.

Candidate context management messages:

``` text
GetDurableContextQuery
GetActiveContextQuery
AddContextCommand
RemoveContextCommand
ReplaceContextCommand
PinContextCommand
ExpireContextCommand
SummarizeContextCommand
```

Context mutation is event sourced. "Removed" means excluded from active
context; immutable historical events remain.

## 9. Context policy and budget

Each skill defines inclusion policy and context budget.

Deterministic priority:

``` text
1. Skill/system instructions
2. Invocation input
3. Pinned context
4. Current business state
5. Highest-priority relevant history
6. Durable summaries of older history
7. Drop low-priority context
```

Persist useful history; inject only relevant history.

As history grows, context summarization may create durable summary
entries while retaining original event-sourced detail.

Every invocation should record skill version, context-policy version and
included context-entry IDs so QTS can answer: **What did this agent know
when it produced this result?**

## 10. Internal QTS tools

Initially the only internal tools are:

``` text
Command Tools
Query Tools
Event Tools
```

Implement them directly as MAF function tools through the QTS Agent Tool
Gateway.

Do not expose unrestricted `ExecuteAnyCommand` or `PublishAnyEvent`
model-visible tools.

Each skill explicitly allow-lists concrete capabilities.

Example first advisor:

``` text
Allowed Commands: none
Allowed Queries: none initially
Allowed Events:
    StrategyWorkflowSummarizedCompleteEvent
    StrategyWorkflowSummarizedFailEvent
```

The gateway may be generic internally; the model-visible surface must be
skill-specific.

### Command semantics

Send a normal QTS command. MAF may receive
`Accepted/Rejected/CommandId/Reason`. Actor processing remains
authoritative.

### Query semantics

MAF can synchronously await an individual QTS query result while its own
invocation is running. This does not block the QTS system on the LLM.

### Event semantics

Prefer semantic tools such as `CompleteStrategyWorkflowSummary(...)` and
`FailStrategyWorkflowSummary(...)`, mapped internally to QTS events.

## 11. MCP boundary

``` text
Internal QTS actor capability → direct MAF function tool
External advisory capability → consider MCP
```

Example external MCP use: QuickBooks/Intuit advisory access.

Authoritative accounting remains:

``` text
QTS → IGeneralLedger → Intuit REST API
```

Advisory accounting may be:

``` text
Accounting AgentSkill → MAF → Intuit MCP → QuickBooks
```

MCP is never required for authoritative General Ledger posting.

## 12. First agent: Strategy Workflow Advisor

Goal: produce a **300--500 word** interpretation of one terminal
Strategy Workflow regardless of how far the deterministic pipeline
progressed.

The advisor: - explains completed pipeline stages; - explains
deterministic denials; - explains failure/stop point; - never changes
the workflow; - never selects a replacement strategy; - never submits an
order; - never overrides PortfolioRisk.

Trigger should semantically mean **summary requested**, not summary
completed.

``` mermaid
flowchart TD
 W[Strategy Workflow Terminal State] --> R[Summary Requested Event]
 R --> E[Event Store]
 E --> P[Agent Invocation Projection]
 P --> Q[Durable Agent Queue]
 Q --> M[MAF Strategy Advisor]
 M --> L[Gemma 4]
 L --> T{QTS Event Tool}
 T -->|Success| C[StrategyWorkflowSummarizedCompleteEvent]
 T -->|Failure| F[StrategyWorkflowSummarizedFailEvent]
 C --> A[Agent Actor / Event Sourcing / Projection / UI]
 F --> A
```

The request context contains the finalized workflow result, including
any available Regime Discovery, Market Condition, Strategy Selector,
Order Composer, Portfolio Risk and Order Execution results plus
failure/termination information.

No raw OTEL/log history should be injected unless explicitly requested
by a future skill policy.

## 13. First advisor context policy

Always include: - skill instructions; - current workflow
identity/status; - all available pipeline results needed for
explanation; - terminal failure/denial information.

Initially exclude: - unrelated workflows; - raw logs/traces; - unrelated
positions; - unrelated account state; - unrelated historical advisor
conversations.

A previous summary for the same horizon may be added later only if
demonstrated useful.

## 14. Failure model

Assume at all times that MAF/LLM may be down.

Actor-side behavior must support: - no result; - late result; - failed
result; - malformed result; - expired result; - duplicate tool call; -
duplicate invocation delivery; - MAF process restart; - LLM process
restart.

Core trading continues independently.

Queries such as `GetAgentInvocationStatusQuery` or
`GetLatestAdvisorSummaryQuery` may inspect durable state later.

Every future skill defines actor-side behavior for `Success`, `Failure`,
`Timeout`, `Unavailable`, and `Expired`.

## 15. Stateless MAF rule

MAF workers are disposable execution hosts.

An invocation must be reconstructable from:

``` text
AgentSkill version
+ Invocation parameters
+ Event-sourced durable context
+ Context policy version
+ Allowed-tool manifest
```

No correctness guarantee may depend on an in-memory MAF session
surviving.

## 16. Future extensibility

The same backbone may later support:

``` text
StrategyAdvisorActor
PositionAdvisorActor
RiskAdvisorActor
BacktestAdvisorActor
ResearchAdvisorActor
AccountingAdvisorActor
```

A future large skill may use: - multiple QTS queries/commands/events; -
MAF sub-agents/parallel agents; - sandboxed code execution; - external
MCP tools; - large durable context; - multi-step research loops.

The actor-facing abstraction remains: **execute Skill X with Invocation
Y**.

Backtesting/research agents may become sophisticated, but deterministic
trading, pricing, risk, order composition and execution remain
production C# capabilities.

## 17. Security and authority

Skills are capability boundaries.

A skill MUST only expose tools required for its responsibility.

No advisor skill gets broker/order/risk-changing commands unless
explicitly designed and approved later.

Model-generated text is untrusted advisory output until translated into
an allowed QTS tool call or stored as advisory content.

External MCP tools must have independent permission policies.

## 18. Observability

Record: - InvocationId; - AgentId; - SkillId/version; - ContextPolicy
version; - context-entry IDs included; - queue latency; - MAF execution
duration; - model identity; - tool calls attempted/completed/failed; -
completion/failure/expiry; - token counts where available; -
trace/correlation IDs.

OTEL/GreptimeDB is operational telemetry. Event-sourced actor state
remains authoritative business/agent history.

## 19. Implementation stages for Codex

Implement incrementally:

1.  QTS `AgentSkillDefinition`, `AgentSkillInvocation`,
    execution/result/status contracts.
2.  AgentActor skeleton and event-sourced invocation state.
3.  Durable AgentContext model and minimal context policy.
4.  `IAgentSkillExecutor` application interface.
5.  Durable agent-work queue abstraction.
6.  QTS Agent Tool Gateway with Event tools only.
7.  MAF adapter and local Gemma 4 execution.
8.  Strategy Workflow Advisor skill.
9.  Complete/Fail event-tool round trip.
10. Query tools.
11. Command tools.
12. Context management commands/queries.
13. Context summarization/versioning.
14. External MCP integrations only when required.

Each stage must compile and have automated tests before the next stage.

## 20. Acceptance criteria

-   Actor code has no hard MAF dependency.
-   QTS skill contract is framework/model neutral.
-   Skill definition and invocation parameters are separate.
-   Agent work is fully asynchronous and durable.
-   Trading continues with MAF/LLM unavailable.
-   Durable context is event sourced.
-   Invocation context is built from policy-selected durable context.
-   MAF has no authoritative durable memory.
-   Internal tools are direct MAF Command/Query/Event function tools.
-   Tools are allow-listed per skill.
-   First Strategy Advisor has only Complete/Fail Event tools.
-   First advisor summarizes partial, denied, successful or failed
    workflows.
-   Complete/Fail returns through normal QTS actor event infrastructure.
-   Invocation/context/skill versions are auditable.
-   External MCP is optional and separate from internal actor tools.
-   No LLM path can bypass deterministic Portfolio Risk or execution
    authority.

## 21. Final architectural rule

> **QTS Agent Actors emulate durable, event-sourced skills. AgentSkill
> is the stable capability contract; AgentSkillInvocation supplies
> parameters and selected durable context; MAF is a stateless/disposable
> interpreter and tool-calling harness; the LLM is an optional reasoning
> engine; all authority and durable state remain in the QTS actor
> system.**
