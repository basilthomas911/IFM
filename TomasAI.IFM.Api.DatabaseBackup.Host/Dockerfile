# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
WORKDIR /src
COPY . .
RUN dotnet publish TomasAI.IFM.Api.DatabaseBackup.Host/TomasAI.IFM.Api.DatabaseBackup.Host.csproj \
    --configuration Release \
    --output /app/publish \
    --no-self-contained \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS runtime
USER root
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends \
        ca-certificates \
        curl \
        postgresql-16 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p \
        /opt/scylla-manager/bin \
        /var/lib/ifm/database-backup/journal \
        /var/lib/ifm/database-backup/online-vault \
        /var/lib/ifm/database-backup/offline-media \
        /var/lib/ifm/database-backup/postgresql \
        /var/lib/ifm/database-backup/scylla \
        /var/lib/ifm/database-restore/workspace \
        /var/lib/ifm/database-restore/postgresql \
        /var/lib/ifm/database-restore/scylla \
    && chown -R app:app /opt/scylla-manager /var/lib/ifm

WORKDIR /app
COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_ENVIRONMENT=Container \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
VOLUME ["/var/lib/ifm/database-backup", "/var/lib/ifm/database-restore"]

USER app
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=6 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "TomasAI.IFM.Api.DatabaseBackup.Host.dll"]
