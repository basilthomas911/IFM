# syntax=docker/dockerfile:1

FROM scylladb/scylla-manager-agent:3.4.2 AS manager-agent
FROM scylladb/scylla:6.2.2

USER root
COPY --from=manager-agent /usr/bin/scylla-manager-agent /usr/bin/scylla-manager-agent
COPY scylla-manager-agent.supervisor.conf /etc/supervisord.conf.d/scylla-manager-agent.conf
RUN mkdir -p /etc/scylla-manager-agent \
    && chmod 0755 /usr/bin/scylla-manager-agent
