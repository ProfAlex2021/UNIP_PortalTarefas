# syntax=docker/dockerfile:1
# =============================================================================
# Roteiro 8 — imagem única da aplicação: API ASP.NET Core + SPA React integrada.
# Build multi-stage com imagens base de versão fixa. Nenhum segredo entra na imagem:
# connection string, senhas e URLs chegam por variáveis de ambiente na execução.
#
#   docker build -t portaltarefas:local --build-arg COMMIT_SHA=$(git rev-parse HEAD) .
# =============================================================================

# ---------- 1) Front-end: npm ci + build do Vite ----------
FROM node:22.23.4-alpine AS frontend
WORKDIR /src/frontend
COPY src/PortalTarefas.Frontend/package.json src/PortalTarefas.Frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/PortalTarefas.Frontend/ ./
RUN npm run build

# ---------- 2) Back-end: restore, build e publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS build
WORKDIR /src
# Restore em camada própria: só é refeito quando o .csproj muda (cache de build).
COPY global.json ./
COPY src/PortalTarefas.Web/PortalTarefas.Web.csproj src/PortalTarefas.Web/
RUN dotnet restore src/PortalTarefas.Web/PortalTarefas.Web.csproj
COPY src/PortalTarefas.Web/ src/PortalTarefas.Web/
# A SPA compilada vira arquivo estático da API, servida em /app/.
COPY --from=frontend /src/frontend/dist/ src/PortalTarefas.Web/wwwroot/app/
# O documento OpenAPI já é versionado em contratos/ e verificado no CI; não é regerado aqui.
RUN dotnet publish src/PortalTarefas.Web/PortalTarefas.Web.csproj \
      --configuration Release --no-restore --output /app/publish \
      -p:OpenApiGenerateDocuments=false -p:UseAppHost=false

# ---------- 3) Execução: só o runtime ASP.NET, sem SDK nem Node ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble AS final

# curl apenas para o HEALTHCHECK do contêiner.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

ARG COMMIT_SHA=desconhecido
LABEL org.opencontainers.image.title="PortalTarefas" \
      org.opencontainers.image.source="https://github.com/" \
      org.opencontainers.image.revision="${COMMIT_SHA}"

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    PORTALTAREFAS_COMMIT=${COMMIT_SHA} \
    DOTNET_NOLOGO=true

EXPOSE 8080

# Usuário NÃO privilegiado já existente nas imagens oficiais do .NET (UID 1654).
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "PortalTarefas.Web.dll"]
