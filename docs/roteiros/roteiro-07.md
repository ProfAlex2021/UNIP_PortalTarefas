# Roteiro 7 — Logging, métricas, tracing, Health Checks, resiliência, rate limiting e output caching

## Pacotes

| Pacote | Versão | Uso |
|---|---|---|
| OpenTelemetry.Extensions.Hosting | 1.19.1 | Integração com o host |
| OpenTelemetry.Instrumentation.AspNetCore / Http / SqlClient / Runtime | 1.19.0 | Instrumentação automática |
| OpenTelemetry.Exporter.Console / OpenTelemetryProtocol | 1.19.1 | Exportação para console ou coletor OTLP |
| Microsoft.Extensions.Http.Resilience | 10.10.0 | Timeout, retry e circuit breaker (Polly) |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.12 | Verificação do banco |

## Arquivos produzidos

| Arquivo | Papel |
|---|---|
| `Observabilidade/Telemetria.cs` | OpenTelemetry (logs, métricas, traces), métricas e span próprios |
| `Saude/ConfiguracaoSaude.cs` | `/health/live` e `/health/ready` com resposta pública enxuta |
| `Resiliencia/ClienteCalendario.cs` | `HttpClient` tipado com timeout, retry e circuit breaker |
| `Resiliencia/SimulacaoCalendario.cs` | Dependência externa simulada (só em Development) com modos de falha |
| `Capacidade/ConfiguracaoCapacidade.cs` | Rate limiting no login e política de output cache |
| `Api/PublicoEndpoints.cs` | Leitura pública e agregada com output cache |
| `Api/V1/CalendarioEndpoints.cs` | Consulta à dependência resiliente (503 se indisponível) |
| `tools/carga/carga.mjs` | Carga controlada |
| `docker-compose.observabilidade.yml` | **Extensão:** painel local (Aspire Dashboard) |
| `tests/.../Roteiro07/*` | Testes de saúde, resiliência, rate limiting, cache e logs |

## Etapa 1 — Logs

- **Estruturados**: mensagens com parâmetros nomeados (`{TarefaId}`, `{Status}`, `{DuracaoMs}`). Em produção
  (`appsettings.json`) o console usa o formatador **JSON** com escopos; em desenvolvimento, linha simples.
- **Correlação**: o middleware do Roteiro 1 abre o escopo `CorrelationId` e devolve `X-Correlation-Id`; os
  Problem Details trazem `correlationId`. Uma falha é localizada procurando esse valor nos logs.
- **Níveis coerentes**: entrada da requisição em `Debug`; saída em `Information` (2xx/3xx), `Warning` (4xx) ou
  `Error` (5xx); retries e conflitos em `Warning`; logs de `HttpClient` reduzidos a `Warning`.
- **Sem dados sensíveis**: apenas caminho (sem query string), nunca corpo, cabeçalhos, cookies ou senhas.
  O login registra só o ID do usuário; tentativas falhas não registram o e-mail digitado.
- **Sem duplicação**: cada operação relevante (ex.: alteração de situação) registra um início e um resultado
  com duração, dentro do escopo `TarefaId`; as demais camadas não repetem o mesmo evento.

## Etapa 2 — Métricas e tracing (OpenTelemetry)

| Sinal | Nome | Tipo | Dimensões (cardinalidade limitada) |
|---|---|---|---|
| Métrica de negócio | `portaltarefas.tarefas.criadas` | contador | `prioridade` (3 valores) |
| Métrica de negócio | `portaltarefas.tarefas.conflitos` | contador | — |
| Métrica técnica | `portaltarefas.painel.duracao` (ms) | histograma | `fonte` (`ef`, `dapper`) |
| Span customizado | `AlterarSituacaoTarefa` | trace | `tarefa.id`, `tarefa.situacao.nova` |

Instrumentação automática: ASP.NET Core, `HttpClient` (inclusive a dependência), SqlClient (comandos sem texto),
runtime, rate limiting, output caching e eventos do Polly.

**Exportação** (sem mudar código):

```powershell
# Console (evidência rápida: spans e métricas no terminal)
$env:Telemetria__ExportarConsole="true"; dotnet run --project src/PortalTarefas.Web --launch-profile https

# Coletor local com painel (extensão)
docker compose -f docker-compose.observabilidade.yml up -d
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"; dotnet run --project src/PortalTarefas.Web --launch-profile https
# abrir http://localhost:18888 (o link com token aparece em: docker compose -f docker-compose.observabilidade.yml logs)
```

No painel, uma requisição a `/api/v1/calendario/feriados/2026` mostra o trace correlacionado: span do ASP.NET Core →
spans do `HttpClient` (uma por tentativa) → span da dependência simulada, e os logs da mesma requisição.

## Etapa 3 — Health Checks

| Endpoint | Finalidade | Verifica | Dependência indisponível |
|---|---|---|---|
| `/health/live` | O processo responde? (reiniciar se não) | nada | **200** Healthy |
| `/health/ready` | Pode receber tráfego? | banco (`DbContext`) e calendário | **503** Unhealthy |

Resultado observado:

```
ready: {"status":"Healthy","verificacoes":{"banco":"Healthy","calendario":"Healthy"}} 200
ready (indisponível): {"status":"Unhealthy","verificacoes":{"banco":"Healthy","calendario":"Unhealthy"}} 503
live  (indisponível): {"status":"Healthy","verificacoes":{}} 200
```

A resposta pública traz só status por verificação — sem mensagens de exceção, servidores ou connection strings.
Os health checks não passam pelo rate limiting e são excluídos dos traces.

## Etapa 4 — Resiliência e capacidade

**HttpClient do calendário** (de fora para dentro): timeout total 10 s → retry (3 tentativas, exponencial com
jitter, **somente GET/HEAD** e **somente falhas transitórias**: 5xx, 408, 429, erro de rede, timeout) →
circuit breaker (abre com 50% de falha em ≥ 4 chamadas numa janela de 30 s; fica aberto 15 s) → timeout por tentativa 2 s.

Logs observados (simulação: 2 falhas transitórias e depois indisponibilidade):

```
warn: Polly  [corr fa78c6] ... EventName: 'OnRetry' ...           <- falha transitória repetida
fail: Polly  [corr 11dff0] ... EventName: 'OnCircuitOpened' ...   <- circuito aberto
warn: ClienteCalendario [corr bca5f0] Serviço de calendário indisponível: BrokenCircuitException  <- falha rápida
```

Tempos: com o circuito aberto, a API devolve 503 em ~20–35 ms, sem chamar a dependência.

**Rate limiting** — política `login`: 5 tentativas por minuto por IP em `POST /api/auth/login`; excesso → **429**
com `Retry-After` e Problem Details. Complementa o bloqueio de conta do Identity (que é por usuário).

**Output caching** — política `painel-publico` só em `GET /api/publico/painel` (dados agregados, públicos e iguais
para todos): 30 s de duração e invalidação pela tag `tarefas` sempre que uma tarefa é criada, editada, muda de
situação ou é excluída. Requisições autenticadas não são guardadas nem servidas do cache.

**Carga controlada** (`node tools/carga/carga.mjs https://localhost:7260 200 10`), nesta máquina:

```
GET /api/publico/painel   200=200   p50=3.8 ms  p95=29.1 ms   200 respostas do output cache
POST /api/auth/login      401=4  429=196   (o 5º acesso da janela já tinha sido usado num login anterior)
GET /health/live          200=200   p50=3.0 ms  p95=4.5 ms
```

Esses números valem só para este teste local (mesma máquina para carga e API, banco LocalDB, modo Development)
e **não** indicam capacidade de produção.

## Extensões implementadas

1. **Painel local** com métricas, traces e logs: Aspire Dashboard via `docker-compose.observabilidade.yml`.
2. **Teste automatizado do rate limiting** (`RateLimitingTests`) com limite configurável.
3. **Alerta conceitual**:

| Alerta | Sinal | Limiar | Ação esperada |
|---|---|---|---|
| API não pronta | `/health/ready` ≠ 200 | 3 verificações seguidas (1 por 30 s) | Tirar a instância do balanceador; verificar banco e calendário pelo `correlationId` dos erros |
| Erros 5xx | `http.server.request.duration` com `http.response.status_code` ≥ 500 | > 2% das requisições em 5 min | Abrir incidente; filtrar logs `Error` pelo `CorrelationId` |
| Circuito aberto | evento Polly `OnCircuitOpened` | qualquer ocorrência | Avisar a equipe; o recurso de feriados fica degradado (503) |
| Ataque ao login | respostas 429 em `/api/auth/login` | > 50 em 5 min | Investigar IPs; considerar bloqueio na borda |

## Testes obrigatórios (relatório, item 4)

| Teste | Onde |
|---|---|
| Erro identificável por correlação sem expor segredo | `LogsCorrelacionadosTests` |
| Dependência indisponível em readiness, liveness mantido | `SaudeTests.Dependencia_indisponivel_afeta_readiness_mas_nao_liveness` |
| Retry só em falha transitória e abertura do circuit breaker | `PipelineResilienciaTests`, `ResilienciaPelaApiTests` |
| 429 após exceder o limite | `RateLimitingTests` |
| Cache em endpoint público e não personalizado | `OutputCacheTests` |
