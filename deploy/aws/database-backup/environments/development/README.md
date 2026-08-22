# Development deployment sequence

Gate 4 infrastructure is prepared but remains mutation-disabled in
`scripts/AwsBackup/gate0-identity-allowlist.json`. No stack may be created until the repository owner reviews the
four templates, supplies the final budget/audit principals, and changes the development mutation flag in a reviewed
commit.

The reviewed change-set order is:

1. `recovery-vault/template.yaml` in `ca-west-1`, using the deterministic replication-role ARN that the primary stack
   will create.
2. `primary-vault/template.yaml` in `ca-central-1`, using the recovery bucket/key outputs.
3. `workload/template.yaml` in `ca-central-1`, using both vault outputs.
4. `policy/audit.yaml` in `ca-central-1`, using the final vault bucket names.

`New-AwsBackupChangeSet.ps1` creates a change set only after the fail-closed STS/account/Region/mutation preflight.
It intentionally never executes a change set. Execution, drift checks, and the disposable Object Lock/replication
test require the independent Gate 4 security and operations approval recorded in the validation report.
