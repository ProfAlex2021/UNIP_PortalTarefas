using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Modelos;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Endpoints;

/// <summary>
/// Endpoints de tarefas com Minimal APIs, demonstrando binding de quatro fontes:
/// rota (id), query string (filtros), cabeçalho (cliente) e corpo JSON (cadastro).
/// Desde o Roteiro 2 os dados vêm de <see cref="ITarefaService"/>.
/// </summary>
public static class TarefasEndpoints
{
    public static IEndpointRouteBuilder MapTarefasEndpoints(this IEndpointRouteBuilder rotas)
    {
        // Legado do Roteiro 1: mantido para comparação, fora do documento OpenAPI (a API oficial é a v1).
        var grupo = rotas.MapGroup("/tarefas").WithTags("Roteiro 1").ExcludeFromDescription();

        grupo.MapGet("/", Listar).WithName("R1_ListarTarefas");
        grupo.MapGet("/{id:int}", ObterPorId).WithName("R1_ObterTarefa");
        grupo.MapPost("/", Cadastrar).WithName("R1_CadastrarTarefa");

        return rotas;
    }

    // GET /api/tarefas?situacao=Pendente&busca=relatorio   (+ cabeçalho opcional X-Cliente-Id)
    private static async Task<Ok<List<TarefaDto>>> Listar(
        [FromQuery] SituacaoTarefa? situacao,
        [FromQuery] string? busca,
        [FromHeader(Name = "X-Cliente-Id")] string? clienteId,
        ITarefaService servico,
        ILogger<Program> logger,
        CancellationToken cancelamento)
    {
        logger.LogInformation("Listagem solicitada pelo cliente {ClienteId}", clienteId ?? "(não informado)");

        var tarefas = await servico.ListarAsync(new FiltroTarefas(situacao, busca), cancelamento);
        return TypedResults.Ok(tarefas.Select(TarefaDto.De).ToList());
    }

    // GET /api/tarefas/5  -> 200 com a tarefa ou 404 Problem Details
    private static async Task<Results<Ok<TarefaDto>, ProblemHttpResult>> ObterPorId(
        [FromRoute] int id, ITarefaService servico, CancellationToken cancelamento)
    {
        if (id <= 0)
            return TypedResults.Problem(statusCode: 400, title: "Identificador inválido.",
                detail: "O id deve ser um número inteiro positivo.");

        var tarefa = await servico.ObterAsync(id, cancelamento);
        return tarefa is not null
            ? TypedResults.Ok(TarefaDto.De(tarefa))
            : TypedResults.Problem(statusCode: 404, title: "Tarefa não encontrada.",
                detail: $"Não existe tarefa com id {id}.");
    }

    // POST /api/tarefas  { "titulo": "...", "descricao": "..." }
    // A validação das Data Annotations ocorre antes (AddValidation) e devolve 400 automaticamente.
    private static async Task<Created<TarefaDto>> Cadastrar(
        [FromBody] NovaTarefaRequest requisicao, ITarefaService servico, CancellationToken cancelamento)
    {
        var criada = await servico.CriarAsync(new Tarefa
        {
            Titulo = requisicao.Titulo.Trim(),
            Descricao = requisicao.Descricao
        }, cancelamento);
        return TypedResults.Created($"/api/tarefas/{criada.Id}", TarefaDto.De(criada));
    }
}
