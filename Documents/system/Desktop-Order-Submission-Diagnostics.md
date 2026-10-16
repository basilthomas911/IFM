# Desktop order submission and diagnostics

Both UI hosts use BrokerManualTradeOrderViewModel and PortfolioTradeOrderService.

## Stages

Confirm resumes submission; it is not broker acknowledgement. Candidate validation resolves the deployment and broker account, then Portfolio evaluates risk/capacity. A NoTradeOrders receipt is a declined submission with fund reason codes. Accepted order IDs remain available even if lifecycle dispatch is unconfirmed. Lifecycle dispatch creates, approves, readies and binds the order execution. Backend event projectors start execution and call TradeBroker.PlaceAsync. Broker receipts and observations establish Working/Rejected, execution fills and completion.

Rust publishes accepted IDs/status before loading execution evidence, selects Order Fills, and polls evidence independently. Failed evidence reads preserve submission status. An unconfirmed mutation is fenced against another submission in the same editor; do not close/reopen to retry it. Reconcile the logged composition/portfolio event/order IDs first. No mutation is automatically retried by the UI. Existing backend handoffs use deterministic command/operation IDs and durable projector replay.

## Execution route

TradeBroker:AccountAlias and TradeBroker:Environment configure the shared desktop route. Development defaults to IFM-EMULATOR-PAPER/Emulator, accepts TradeBroker:Emulator:AccountAlias as an alias override, and rejects Paper/Live. Non-Development requires an explicit configured route. The queried account environment must match the configured route; normal qualification and capacity checks remain in force.

## Logs

All added stage records start with OrderSubmission. Client records correlate SubmissionId (the composition ID), setup portfolio/fund/order/trade IDs, portfolio operation/completion event ID, accepted order ID, account and environment. Backend records add execution attempt, broker component/order ID, operation ID, source event ID, broker receipt category/detail, observation kind, status and fill quantity. Lifecycle command results include the exact stage, command ID, error code and message. These are stage results, not implied fills.

WinForms uses its existing DiagnosticLogger sinks. Rust now writes JSON Lines to managed/logs/rust-ui-<process-id>.jsonl under the deployed app, or IFM_UI_DIAGNOSTIC_LOG if provided. Rust retains structured fields, timestamps and correlation scopes. Server stage records use the existing API logger/telemetry sinks. Read refreshes log failures and changed evidence rather than every successful one-second poll.

No credentials or full candidate/account payloads are added to these logs.

## Verification for this change

Focused coverage: 14 shared service/route tests, 15 Rust Trade Blotter tests, four Strategy Workspace tests, one structured client-log test, and seven actor/emulator workflow tests passed. The actor/emulator tests execute production handlers/projectors with in-memory transport/storage ports, not a running API.

The separate isolated hosted NATS test remains unqualified: the old TradeDb/TradePlanDb/SecuritiesDb fixture connection routing was corrected to PostgreSQL, after which the hosted run failed and cleanup reported disposed actor transport semaphores. This is not recorded as an end-to-end pass. No order was placed against the user's Development session or a real broker during these checks.

The currently running API/UI must be restarted through the Development scripts to load the changed binaries; the logs added here do not reconstruct a prior submission attempt.
