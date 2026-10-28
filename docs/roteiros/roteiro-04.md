# Roteiro 4 — Recursos REST, paginação, filtros, Problem Details, OpenAPI e cliente TypeScript

## Como executar

```bash
dotnet run --project src/PortalTarefas.Web --launch-profile https
# Documento OpenAPI:  https://localhost:7260/openapi/v1.json
# Interface Scalar:   https://localhost:7260/scalar   (somente em Development)
dotnet test --filter "FullyQualifiedName~Roteiro04"
```

Requisições de evidência: itens 15 a 25 de `src/PortalTarefas.Web/PortalTarefas.Web.http`.

## Etapa 1 — Contrato

O contrato foi escrito antes do código em [`docs/contrato-api-v1.md`](../contrato-api-v1.md): URIs, métodos,
códigos de status, DTOs, parâmetros de paginação/ordenação/filtro e formato de erro.
Os DTOs ficam em `Api/V1/Contratos.cs` e **a entidade `Tarefa` nunca é serializada** (verificado por teste).

## Etapa 2 — Implementação

| Arquivo | Papel |
|---|---|
| `Api/V1/TarefasApiEndpoints.cs` | Grupo `/api/v1/tarefas` (GET, GET por id, POST, PUT, DELETE) |
| `Api/V1/TarefasApiServico.cs` | Serviço de aplicação: filtros, ordenação por lista permitida e paginação **no banco** |
| `Api/V1/Contratos.cs` | DTOs com Data Annotations, `IValidatableObject` e descrições para o OpenAPI |
| `Dominio/RegrasPrazo.cs` | Regra de prazo compartilhada entre a API e a interface MVC |
| `Infraestrutura/ProblemDetailsPadronizados.cs` | 400 com título em português e `errors` em camelCase |

- POST devolve **201** com `Location` (`CreatedAtRoute`).
- Filtros, `OrderBy`, `Skip` e `Take` são compostos sobre `IQueryable`, então o SQL já sai paginado.
- `ordenarPor` só aceita valores da lista; o texto do cliente nunca é concatenado em SQL.
- Conflito de concorrência no PUT → **409** com a extensão `versaoAtual`.
- Problem Details: só extensões úteis e não sensíveis (`traceId`, `correlationId`, `errors`, `versaoAtual`).

## Etapa 3 — OpenAPI

- `Microsoft.AspNetCore.OpenApi` 10.0.12 com `AddOpenApi`/`MapOpenApi` (em `Api/DocumentacaoOpenApi.cs`).
- **Scalar** (`Scalar.AspNetCore`) em `/scalar`, mapeado apenas em `Development`.
- Descrições com `WithSummary`, `WithDescription` e `[Description]` nos DTOs; cabeçalhos de paginação
  documentados por um *operation transformer*. O gerador nativo do .NET 10 não oferece exemplos por atributo,
  por isso exemplos ficaram no arquivo `.http`.

**Revisão do documento gerado (problemas encontrados e corrigidos):**

| Problema no documento | Correção |
|---|---|
| Faltavam 404 (GET/PUT/DELETE) e 409 (PUT) | `.ProducesProblem(404)` e `.ProducesProblem(409)` |
| Parâmetros em PascalCase (`Pagina`, `Tamanho`) | `[FromQuery(Name = "pagina")]` etc. |
| Inteiros descritos como `integer \| string` | `JsonNumberHandling.Strict` nas opções JSON |
| `descricao` e `prazo` obrigatórios no cadastro | Valores padrão no `record` (tornam-se opcionais) |
| Endpoints de comparação do Roteiro 1 misturados à API | `ExcludeFromDescription()` / `[ApiExplorerSettings(IgnoreApi = true)]` |

## Etapa 4 — Cliente TypeScript (NSwag)

O documento é **gerado a cada build** (`Microsoft.Extensions.ApiDescription.Server`) e versionado em
[`contratos/PortalTarefas.Web.json`](../../contratos/PortalTarefas.Web.json).
O NSwag (ferramenta local `nswag.consolecore` 14.7.1 no `dotnet-tools.json`) lê esse arquivo usando
`src/PortalTarefas.Frontend/nswag.json` e gera `src/api/gerado/portalTarefasClient.ts`.

```bash
cd src/PortalTarefas.Frontend
npm install
npm run gerar-cliente     # regenera o cliente a partir do contrato
npm run verificar-tipos   # o TypeScript valida o uso do cliente
npm run chamada-real      # chamada real à API (precisa da API rodando)
```

`chamada-real` exporta o certificado HTTPS de desenvolvimento (somente a parte pública) para `.cert/`,
porque o Node não usa o repositório de certificados do Windows.

**Quando o contrato mudar:** compilar o projeto Web (atualiza `contratos/`), revisar o diff do JSON,
executar `npm run gerar-cliente` e `npm run verificar-tipos`. Mudanças compatíveis (ex.: novo campo opcional)
não quebram a compilação; mudanças incompatíveis (ex.: renomear um campo) fazem o `tsc` acusar os pontos afetados.
O teste `Documento_em_execucao_e_igual_ao_snapshot_versionado` falha se alguém mudar a API sem atualizar o contrato.

## Extensões implementadas

- **Testes de contrato/snapshot** do documento OpenAPI (`ContratoOpenApiTests`).
- **Metadados de paginação** em cabeçalhos `X-Total-Count` e `Link` (RFC 8288), documentados no OpenAPI e expostos no CORS.

## Testes obrigatórios (relatório, item 4)

| Teste | Onde |
|---|---|
| Paginação com limites mínimo e máximo | `Paginacao_fora_dos_limites_retorna_400`, `Paginacao_nos_limites_*` |
| Ordenação permitida e rejeição de campo não permitido | `Ordenacao_por_campo_*` |
| Combinação de dois filtros | `Combinacao_de_dois_filtros` |
| Problem Details para 400, 404 e 409 | `Post_invalido_*`, `Recurso_inexistente_*`, `Put_com_versao_antiga_*` |
| Regeneração do cliente após mudança compatível | `npm run gerar-cliente` + `npm run verificar-tipos` (procedimento acima) |
