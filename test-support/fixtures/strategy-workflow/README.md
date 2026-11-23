# Ready futures workflow fixture

`ready-futures-no-trade-view.msgpack` is synthetic evidence from `WorkflowDecisionBatchTests.All_five_real_operators_consume_ready_inputs_without_io_and_a_rejected_fund_has_no_entry_effect`. It runs the real EMA, market assessment, selection, composition and financial eligibility models against prepared test inputs. The empty financial fund set produces No Trade and creates no orders or reservations. It contains no live account or quote data.

The PostgreSQL qualification projects this completed workflow, checks assignment-scoped risk history and verifies the large v1 approval fingerprint survives storage. Refresh the fixture by setting `IFM_PREPARED_WORKFLOW_BENCHMARK_FIXTURES` to an output directory, running that test, then copying its `ready-futures-no-trade-view.msgpack` here. Review hash and projection test results before replacing it.
