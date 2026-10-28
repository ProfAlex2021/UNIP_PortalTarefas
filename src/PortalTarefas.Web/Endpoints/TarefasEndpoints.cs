using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Modelos;

namespace PortalTarefas.Web.Endpoints;

/// <summary>
/// Endpoints de tarefas com Minimal APIs, demonstrando binding de quatro fontes:
/// rota (id), query string (filtros), cabeçalho (cliente) e corpo JSON (cadastro).
/// Nesta etapa os dados ficam em memória; a persistência chega no Roteiro 3.
/// </summary>
public static class TarefasEndpoints
{
    private static readonly ConcurrentDictionary<int, TarefaDto> Tarefas = new(
        new Dictionary<int, TarefaDto>
        {
            [1] = new(1, "Configurar ambiente .NET 10", "Instalar SDK e Visual Studio 2026", SituacaoTarefa.Concluida),
            [2] = new(2, "Estudar pipeline HTTP", "Ler a Unidade I", SituacaoTarefa.EmAndamento),
            [3] = new(3, "Entregar relatório do Roteiro 1", null, SituacaoTarefa.Pendente),
        });

    private static int _ultimoId = 3;

    public static IEndpointRouteBuilder MapTarefasEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/tarefas").WithTags("Tarefas");

        grupo.MapGet("/", Listar).WithName("ListarTarefas");
        grupo.MapGet("/{id:int}", ObterPorId).WithName("ObterTarefa");
        grupo.MapPost("/", Cadastrar).WithName("CadastrarTarefa");

        return rotas;
    }

    // GET /api/tarefas?situacao=Pendente&busca=relatorio   (+ cabeçalho opcional X-Cliente-Id)
    private static Ok<List<TarefaDto>> Listar(
        [FromQuery] SituacaoTarefa? situacao,
        [FromQuery] string? busca,
        [FromHeader(Name = "X-Cliente-Id")] string? clienteId,
        ILogger<Program> logger)
    {
        logger.LogInformation("Listagem solicitada pelo cliente {ClienteId}", clienteId ?? "(não informado)");

        var consulta = Tarefas.Values.AsEnumerable();
        if (situacao is not null)
            consulta = consulta.Where(t => t.Situacao == situacao);
        if (!string.IsNullOrWhiteSpace(busca))
            consulta = consulta.Where(t => t.Titulo.Contains(busca, StringComparison.OrdinalIgnoreCase));

        return TypedResults.Ok(consulta.OrderBy(t => t.Id).ToList());
    }

    // GET /api/tarefas/5  -> 200 com a tarefa ou 404 Problem Details
    private static Results<Ok<TarefaDto>, ProblemHttpResult> ObterPorId([FromRoute] int id)
    {
        if (id <= 0)
            return TypedResults.Problem(statusCode: 400, title: "Identificador inválido.",
                detail: "O id deve ser um número inteiro positivo.");

        return Tarefas.TryGetValue(id, out var tarefa)
            ? TypedResults.Ok(tarefa)
            : TypedResults.Problem(statusCode: 404, title: "Tarefa não encontrada.",
                detail: $"Não existe tarefa com id {id}.");
    }

    // POST /api/tarefas  { "titulo": "...", "descricao": "..." }
    // A validação das Data Annotations ocorre antes (AddValidation) e devolve 400 automaticamente.
    private static Created<TarefaDto> Cadastrar([FromBody] NovaTarefaRequest requisicao)
    {
        var id = Interlocked.Increment(ref _ultimoId);
        var tarefa = new TarefaDto(id, requisicao.Titulo.Trim(), requisicao.Descricao, SituacaoTarefa.Pendente);
        Tarefas[id] = tarefa;
        return TypedResults.Created($"/api/tarefas/{id}", tarefa);
    }
}
