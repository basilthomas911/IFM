# ScyllaDB Development Container

This composition replaces only the container definition for the current ScyllaDB 6.2.2 node. It retains the external
`docker_scylla` data volume and adds the compatible Scylla Manager Agent 3.4.2. The fixed development address is used
because Manager must be able to reach the node address reported by Scylla; the prior loopback broadcast address cannot
be managed from another container.

The Agent configuration is a development secret stored outside Git at
`E:\IFM\DatabaseBackup\secrets\scylla-manager-agent.yaml`. The Agent sends backups to the development MinIO endpoint
defined by `Docker/ScyllaManager`; MinIO persists its object data at
`E:\IFM\DatabaseBackup\scylla-manager\object-storage`.

Do not adopt this compose definition until the safety-copy and rollback steps in `Docker/ScyllaManager/README.md`
have passed. Never use `down --volumes`.
