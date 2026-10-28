# Roteiro 8 — Configuração por ambientes, segredos, contêineres e pipeline reprodutível

## Arquivos produzidos

| Arquivo | Papel |
|---|---|
| `appsettings.json` / `appsettings.Development.json` / `appsettings.Production.json` | Valores comuns e diferenças por ambiente (sem segredos) |
| `Configuracao/OpcoesAplicacao.cs` | Opções tipadas validadas na inicialização (`ValidateOnStart`) |
| `.env.example` | Todas as variáveis necessárias, **sem valores secretos** |
| `Dockerfile` | Build multi-stage: Node → SDK .NET → runtime ASP.NET, usuário não privilegiado, `HEALTHCHECK` |
| `.dockerignore` | Mantém fora da imagem `bin/obj`, `node_modules`, `.git`, `.env`, testes e docs |
| `docker-compose.yml` | Composição local: SQL Server + aplicação, segredos vindos de `.env` |
| `.github/workflows/ci-cd.yml` | CI (PR e `main`) e CD (imagem, SBOM, teste, promoção com aprovação) |
| `tests/.../Roteiro08/*` | Validação de configuração, versão implantada e falha deliberada |

## Etapa 1 — Configuração

Precedência (da menor para a maior): `appsettings.json` → `appsettings.{Ambiente}.json` → User Secrets
(somente Development) → variáveis de ambiente → argumentos de linha de comando.

| Onde | O que fica |
|---|---|
| `appsettings.json` | Comum: logs em JSON, limites de login, telemetria |
| `appsettings.Development.json` | Migrations automáticas, simulação, CORS para o Vite, usuários de teste (sem senha) |
| `appsettings.Production.json` | Sem migrations automáticas, simulação desligada, confiança no proxy reverso |
| User Secrets (dev) / variáveis de ambiente (contêiner, CI) | Connection string, senhas de seed — **nunca** no repositório |

Validação na inicialização: `ConnectionStrings:PortalTarefas`, `Dependencias:Calendario:UrlBase` e
`Limites:Login:*` são verificados antes de a aplicação aceitar requisições. Exemplo real, sem connection string:

```
OptionsValidationException: DataAnnotation validation failed for 'OpcoesConexao' members: 'PortalTarefas'
with the error: 'Defina ConnectionStrings:PortalTarefas (variável de ambiente ConnectionStrings__PortalTarefas ou User Secrets).'
```

A mensagem diz **qual** chave falta e **como** fornecê-la, sem mostrar nenhum valor. A mesma imagem sobe em
desenvolvimento ou em produção simulada apenas trocando variáveis — o código não muda.

## Etapa 2 — Contêiner

- **Multi-stage com versões fixas**: `node:22.23.4-alpine` (build do React), `dotnet/sdk:10.0.403-noble`
  (restore/publish), `dotnet/aspnet:10.0.14-noble` (execução). A imagem final não tem SDK, Node nem código-fonte.
- **Restore em camada própria**: alterações no código não invalidam o cache de pacotes.
- **Usuário não privilegiado**: `USER $APP_UID` (UID 1654 das imagens oficiais).
- **Porta e saúde**: `ASPNETCORE_HTTP_PORTS=8080`, `EXPOSE 8080`, `HEALTHCHECK` em `/health/live`.
- **Sem segredos na imagem**: só `ASPNETCORE_ENVIRONMENT=Production`, a porta e o commit; o resto chega na execução.
- **Rastreabilidade**: `--build-arg COMMIT_SHA` vira o rótulo `org.opencontainers.image.revision` e a variável
  `PORTALTAREFAS_COMMIT`, exibida em `GET /versao`.

### Implantação integrada x separada

| Critério | Integrada (escolhida): API serve o build do React em `/app/` | Separada: imagem/serviço próprio para a SPA (CDN, Nginx) |
|---|---|---|
| Origem | Mesma origem → cookie `SameSite=Strict`, sem CORS | Origens diferentes → CORS com credenciais ou token |
| Artefatos e pipeline | Uma imagem, uma versão | Duas imagens e duas versões para coordenar |
| Escala e cache | Escala junto com a API | SPA estática em CDN, escala independente |
| Complexidade | Menor | Maior |

**Decisão:** integrada. Mantém o cookie seguro do Roteiro 6 sem abrir CORS com credenciais, e um commit
gera exatamente um artefato. A SPA fica em `/app/` porque `/tarefas` já é rota da interface MVC; o
`MapFallbackToFile("app/{*caminho:nonfile}", "app/index.html")` permite abrir `/app/tarefas/3` direto.

### Como executar localmente

```bash
copy .env.example .env            # preencha MSSQL_SA_PASSWORD e SEED_ADMIN_SENHA
docker compose up -d --build
docker compose ps                 # ambos "healthy"
curl http://localhost:8080/health/ready
curl http://localhost:8080/versao
# navegador: http://localhost:8080/app/  (login: admin@portaltarefas.local + SEED_ADMIN_SENHA)
docker compose down               # acrescente -v para apagar o volume do banco
```

## Etapa 3 — CI (`.github/workflows/ci-cd.yml`)

| Gatilho | O que roda |
|---|---|
| Pull request para `main` | backend + frontend (sem publicação) |
| Push na `main` | backend + frontend → publicar → implantar-teste → promover-producao (com aprovação) |
| Manual (`workflow_dispatch`) | Igual ao push; a opção `falha_deliberada` força a falha de um teste |

- **Back-end**: SDK pelo `global.json`; SQL Server em contêiner efêmero com senha gerada na execução;
  `dotnet restore` → `build` → verificação de que `contratos/` está atualizado → `dotnet test` → análise de
  pacotes vulneráveis (bloqueia severidade alta/crítica).
- **Front-end**: Node 22.23.4, `npm ci` → `npm test` → `npm run build` → `npm audit --audit-level=high`.
- **Gate**: o job `publicar` tem `needs: [backend, frontend]`. Uma falha em qualquer teste encerra o
  workflow antes da imagem ser publicada.

## Etapa 4 — CD e operação

1. **Imagem com tag = SHA do commit** (`ghcr.io/<dono>/portaltarefas:<sha>`), publicada no GHCR com o
   `GITHUB_TOKEN` (credencial de curta duração emitida para a execução — nenhuma senha longa armazenada).
2. **Ambiente de teste** (`environment: teste`): banco e aplicação em contêineres efêmeros no runner, migrations,
   smoke test (`/health/ready`, `/health/live`, `/api/publico/painel`, `/app/`, 401 em `/api/v1/tarefas`) e
   conferência de que `/versao` devolve o commit implantado.
3. **Promoção** (`environment: producao`): requer aprovação manual (configure *Required reviewers* em
   *Settings → Environments → producao*). Promove **a mesma imagem testada**, sem rebuild, aplicando a tag `producao`.
4. **Rollback**: reaplicar a tag `producao` à imagem de um commit anterior
   (`docker buildx imagetools create --tag ...:producao ...:<sha-anterior>`) e reiniciar o serviço.
   A versão em execução é sempre consultável em `GET /versao`.

Antes de publicar no GitHub, crie os environments `teste` e `producao` (este com revisores obrigatórios).

## Extensões implementadas

- **SBOM** (`anchore/sbom-action`, formato SPDX) publicado como artefato do workflow.
- **Análise de vulnerabilidades** com política de bloqueio: NuGet e npm falham em severidade alta ou crítica
  (as médias ficam visíveis no log sem bloquear, para não travar a entrega por avisos sem correção disponível).
  Durante o Roteiro 4 essa análise já apontou e corrigimos `System.Security.Cryptography.Pkcs` vulnerável.
- **Staging com aprovação antes de produção** (`environment: producao`).

## Testes obrigatórios (relatório, item 4)

| Teste | Como |
|---|---|
| Build limpo do back-end e do front-end a partir de clone novo | `git clone` → `dotnet build` / `npm ci && npm run build` (o CI faz isso a cada execução) |
| Contêiner com configuração externa e sem segredo na imagem | `docker compose up`; `docker image inspect portaltarefas:local` mostra só variáveis não secretas |
| Health Check do serviço em execução | `docker compose ps` (healthy) e `curl http://localhost:8080/health/ready` |
| Falha deliberada bloqueando a publicação | *Actions → CI/CD → Run workflow* com `falha_deliberada` marcado |
| Execução do pipeline para uma alteração válida e rastreabilidade | Push na `main` → imagem `:<sha>` → `/versao` do ambiente de teste mostra o mesmo SHA |
