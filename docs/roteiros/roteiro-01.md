# Roteiro 1 — Pipeline HTTP, middleware, roteamento, binding e Minimal APIs x MVC

## Como executar

```bash
dotnet --version          # registrar no relatório (ex.: 10.0.401)
dotnet run --project src/PortalTarefas.Web --launch-profile https
dotnet test               # testes de integração do pipeline e dos endpoints
```

No Visual Studio 2026: abrir `PortalTarefas.slnx`, escolher o perfil **https** e pressionar F5.
As requisições de evidência estão em `src/PortalTarefas.Web/PortalTarefas.Web.http`.

## Arquivos produzidos

| Arquivo | Papel |
|---|---|
| `Program.cs` | Registro de serviços e ordem do pipeline |
| `Middlewares/RegistroRequisicaoMiddleware.cs` | Middleware próprio (método, caminho, status, duração) |
| `Middlewares/CorrelacaoMiddleware.cs` | **Extensão:** identificador de correlação devolvido em cabeçalho |
| `Infraestrutura/TratadorExcecoesGlobal.cs` | `IExceptionHandler` que gera Problem Details 500 sem detalhes internos |
| `Endpoints/TarefasEndpoints.cs` | Minimal APIs com binding de rota, query, cabeçalho e corpo |
| `Endpoints/DiagnosticoEndpoints.cs` | Endpoint de diagnóstico em Minimal API |
| `Controllers/DiagnosticoController.cs` | Endpoint equivalente em controller MVC |
| `tests/.../Roteiro01/PipelineEEndpointsTests.cs` | **Extensão:** testes de integração |

## Ordem do pipeline implementado

1. `UseExceptionHandler` — envolve todo o restante; captura exceções e gera Problem Details.
2. `UseStatusCodePages` — dá corpo Problem Details a respostas de erro sem corpo.
3. `UseHsts` — somente fora de desenvolvimento.
4. `UseHttpsRedirection` — redireciona HTTP para HTTPS.
5. `UseCorrelacao` — define o `X-Correlation-Id` e abre o escopo de log.
6. `UseRegistroRequisicao` — registra a entrada (`-->`) e a saída (`<--`).
7. `UseStaticFiles` — se o arquivo existir em `wwwroot`, encerra a requisição aqui (curto-circuito).
8. `UseRouting` — seleciona o endpoint.
9. `UseCors("FrontendLocal")` — aplica a política somente à origem `http://localhost:5173`.
10. Endpoints — `MapTarefasEndpoints`, `MapDiagnosticoEndpoints`, `MapControllers`.

Nos logs, a entrada de uma requisição aparece antes da execução do endpoint e a saída depois,
mostrando que o middleware executa "na ida" e "na volta". Uma exceção lançada no endpoint
aparece como "interrompida por exceção" e é transformada em 500 pelo item 1.

## Binding demonstrado (quatro fontes)

| Fonte | Exemplo | Parâmetro |
|---|---|---|
| Rota | `GET /api/tarefas/1` | `[FromRoute] int id` |
| Query string | `GET /api/tarefas?situacao=Pendente&busca=relat` | `[FromQuery] SituacaoTarefa? situacao` |
| Cabeçalho | `X-Cliente-Id: navegador-aluno` | `[FromHeader(Name = "X-Cliente-Id")] string? clienteId` |
| Corpo JSON | `POST /api/tarefas` | `[FromBody] NovaTarefaRequest requisicao` |

Status: 200 (sucesso), 201 + `Location` (cadastro), 400 (entrada inválida), 404 (inexistente), 500 (exceção).

## Comparação Minimal API x MVC

| Critério | Minimal API | MVC (controller) |
|---|---|---|
| Organização | Funções agrupadas por `MapGroup` em classes estáticas | Classe por recurso, ações com atributos |
| Dependências | Injetadas por parâmetro em cada handler | Injetadas no construtor do controller |
| Validação | `AddValidation()` (.NET 10) valida Data Annotations | `[ApiController]` valida via `ModelState` |
| Filtros | Endpoint filters (`AddEndpointFilter`) | Filtros de ação, resultado, exceção, autorização |
| Testes | Integração com `WebApplicationFactory`; handlers estáticos testáveis | Integração ou unidade instanciando o controller |
| Código | Menos cerimônia, menos arquivos | Mais estrutura, convenções conhecidas |

**Decisão para o projeto:** a API HTTP do Portal de Tarefas será construída com **Minimal APIs**
organizadas em grupos (`MapGroup`), porque o contrato é pequeno, a equipe é reduzida e o
.NET 10 já oferece validação, OpenAPI e filtros para esse modelo. **MVC com Razor** será usado
na interface server-side do Roteiro 2, em que views, layouts, Tag Helpers e filtros de ação
são o ponto forte do padrão.

## Testes obrigatórios (relatório, item 4) e onde estão

| Teste | Evidência |
|---|---|
| Parâmetro de rota válido e inválido | `.http` itens 4–6; `Obter_por_id_*` |
| Filtro por query string e cabeçalho opcional | `.http` item 2; `Filtro_por_query_string_e_cabecalho_opcional` |
| POST válido e inválido | `.http` itens 7–8; `Post_*` |
| Exceção controlada sem detalhes sensíveis | `.http` item 11; `Excecao_e_convertida_em_500_sem_detalhes_internos` |
| CORS só para a origem autorizada | `.http` itens 12–13; `Cors_aceita_somente_origem_autorizada` |
