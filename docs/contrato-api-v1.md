# Contrato da API v1 — Tarefas

Definido antes da implementação (Roteiro 4, Etapa 1). O documento OpenAPI gerado
(`contratos/PortalTarefas.Web.json`) é a fonte de verdade executável deste contrato.

Base: `/api/v1/tarefas` · Formato: JSON (enums como texto) · Erros: Problem Details (RFC 9457)

## Recursos e operações

| Método | URI | Corpo | Sucesso | Erros |
|---|---|---|---|---|
| GET | `/api/v1/tarefas` | — | 200 `PaginaTarefas` | 400 parâmetros inválidos |
| GET | `/api/v1/tarefas/{id}` | — | 200 `TarefaDetalhe` | 404 |
| POST | `/api/v1/tarefas` | `TarefaCriacao` | 201 `TarefaDetalhe` + `Location` | 400 validação |
| PUT | `/api/v1/tarefas/{id}` | `TarefaAtualizacao` (com `versao`) | 200 `TarefaDetalhe` (nova `versao`) | 400, 404, **409 conflito** |
| DELETE | `/api/v1/tarefas/{id}` | — | 204 | 404 |

- GET, PUT e DELETE são idempotentes; POST não é.
- PUT substitui todos os campos editáveis e exige a `versao` recebida no GET (concorrência otimista).
  Em 409, o Problem Details traz a extensão `versaoAtual`, que permite reler e decidir.

## DTOs (a entidade persistida nunca é exposta)

| DTO | Uso | Campos |
|---|---|---|
| `TarefaCriacao` | entrada do POST | titulo, descricao?, prioridade, prazo? |
| `TarefaAtualizacao` | entrada do PUT | titulo, descricao?, prioridade, prazo?, situacao, versao |
| `TarefaResumo` | itens da listagem | id, titulo, situacao, prioridade, prazo?, vencida |
| `TarefaDetalhe` | GET por id, POST, PUT | id, titulo, descricao?, situacao, prioridade, prazo?, criadaEm, atualizadaEm?, versao |
| `PaginaTarefas` | listagem | itens, pagina, tamanho, totalItens, totalPaginas |

## Parâmetros da listagem

| Parâmetro | Padrão | Regras |
|---|---|---|
| `pagina` | 1 | inteiro ≥ 1 |
| `tamanho` | 10 | 1 a **50** (limite máximo) |
| `ordenarPor` | `id` | lista permitida: `id`, `titulo`, `prazo`, `prioridade`, `situacao`, `criadaEm` |
| `direcao` | `asc` | `asc` ou `desc` |
| `situacao` | — | `Pendente`, `EmAndamento`, `Concluida` |
| `prioridade` | — | `Baixa`, `Media`, `Alta` |
| `busca` | — | texto contido no título (até 100 caracteres) |

Filtros, ordenação e paginação são aplicados **no banco**, antes de materializar a consulta.
Fora dos limites ou com campo de ordenação não permitido → 400 com `errors` por parâmetro.

## Metadados de paginação (extensão)

Além do corpo, a listagem devolve os cabeçalhos:

- `X-Total-Count`: total de itens que atendem aos filtros;
- `Link`: URIs `first`, `prev`, `next` e `last` (RFC 8288), preservando filtros e ordenação.

## Problem Details

Campos padrão `type`, `title`, `status`, `detail`, `instance` e as extensões não sensíveis:
`traceId`, `correlationId` (todas as respostas de erro), `errors` (400) e `versaoAtual` (409).
Nunca são incluídos stack trace, mensagens de exceção, SQL ou dados de conexão.
