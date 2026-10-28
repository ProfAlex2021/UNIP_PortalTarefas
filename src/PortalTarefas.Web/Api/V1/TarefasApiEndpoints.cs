using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Api.V1;

/// <summary>
/// Recurso REST /api/v1/tarefas. O contrato está em docs/contrato-api-v1.md e no
/// documento OpenAPI gerado em contratos/PortalTarefas.Web.json.
/// </summary>
public static class TarefasApiEndpoints
{
    public const string NomeObterTarefa = "ObterTarefa";

    public static IEndpointRouteBuilder MapTarefasApiV1(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/v1/tarefas")
            .WithTags("Tarefas")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        grupo.MapGet("/", Listar)
            .WithName("ListarTarefas")
            .WithSummary("Lista tarefas com filtros, ordenação e paginação")
            .WithDescription("Filtros, ordenação e paginação são aplicados no banco. Devolve também os cabeçalhos X-Total-Count e Link.")
            .ProducesValidationProblem()
            .AddOpenApiOperationTransformer(DocumentacaoOpenApi.DocumentarCabecalhosDePaginacao);

        grupo.MapGet("/{id:int}", Obter)
            .WithName(NomeObterTarefa)
            .WithSummary("Obtém uma tarefa pelo identificador")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/", Criar)
            .WithName("CriarTarefa")
            .WithSummary("Cadastra uma tarefa")
            .WithDescription("Devolve 201 com o cabeçalho Location apontando para a nova tarefa.")
            .ProducesValidationProblem();

        grupo.MapPut("/{id:int}", Atualizar)
            .WithName("AtualizarTarefa")
            .WithSummary("Atualiza todos os campos editáveis de uma tarefa")
            .WithDescription("Exige a 'versao' obtida no GET. Se a tarefa foi alterada por outra pessoa, devolve 409 com 'versaoAtual'.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:int}", Excluir)
            .WithName("ExcluirTarefa")
            .WithSummary("Exclui uma tarefa")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return rotas;
    }

    private static async Task<Ok<PaginaTarefas>> Listar(
        [AsParameters] ParametrosConsultaTarefas parametros,
        TarefasApiServico api,
        HttpContext contexto,
        CancellationToken cancelamento)
    {
        var pagina = await api.ListarAsync(parametros, cancelamento);
        Paginacao.EscreverCabecalhos(contexto, pagina);
        return TypedResults.Ok(pagina);
    }

    private static async Task<Results<Ok<TarefaDetalhe>, ProblemHttpResult>> Obter(
        int id, TarefasApiServico api, CancellationToken cancelamento)
    {
        var tarefa = await api.ObterAsync(id, cancelamento);
        return tarefa is null ? Problemas.NaoEncontrada(id) : TypedResults.Ok(tarefa);
    }

    private static async Task<Results<CreatedAtRoute<TarefaDetalhe>, ValidationProblem>> Criar(
        TarefaCriacao dto, TarefasApiServico api, CancellationToken cancelamento)
    {
        // As Data Annotations e IValidatableObject já foram verificadas por AddValidation();
        // aqui fica apenas a regra que depende de dados.
        if (await api.TituloEmUsoAsync(dto.Titulo, null, cancelamento))
            return Problemas.TituloDuplicado();

        var criada = await api.CriarAsync(dto, cancelamento);
        return TypedResults.CreatedAtRoute(criada, NomeObterTarefa, new { id = criada.Id });
    }

    private static async Task<Results<Ok<TarefaDetalhe>, ValidationProblem, ProblemHttpResult>> Atualizar(
        int id, TarefaAtualizacao dto, TarefasApiServico api, CancellationToken cancelamento)
    {
        if (!TentarLerVersao(dto.Versao, out var versao))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["versao"] = ["Versão inválida: use o valor recebido no GET."]
            });

        if (await api.TituloEmUsoAsync(dto.Titulo, id, cancelamento))
            return Problemas.TituloDuplicado();

        var (resultado, tarefa) = await api.AtualizarAsync(id, dto, versao, cancelamento);
        return resultado switch
        {
            ResultadoOperacao.Sucesso => TypedResults.Ok(tarefa!),
            ResultadoOperacao.Conflito => Problemas.Conflito(tarefa!.Versao),
            _ => Problemas.NaoEncontrada(id)
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Excluir(
        int id, TarefasApiServico api, CancellationToken cancelamento) =>
        await api.ExcluirAsync(id, cancelamento) ? TypedResults.NoContent() : Problemas.NaoEncontrada(id);

    private static bool TentarLerVersao(string base64, out byte[] versao)
    {
        var buffer = new byte[base64.Length];
        var ok = Convert.TryFromBase64String(base64, buffer, out var tamanho) && tamanho > 0;
        versao = ok ? buffer[..tamanho] : [];
        return ok;
    }

    /// <summary>Problem Details padronizados da API. Só extensões úteis e não sensíveis.</summary>
    private static class Problemas
    {
        public static ProblemHttpResult NaoEncontrada(int id) => TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Tarefa não encontrada.",
            detail: $"Não existe tarefa com id {id}.");

        public static ProblemHttpResult Conflito(string versaoAtual) => TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "A tarefa foi alterada por outra pessoa.",
            detail: "Nenhuma alteração foi gravada. Obtenha a tarefa novamente, revise e reenvie com a versão atual.",
            extensions: new Dictionary<string, object?> { ["versaoAtual"] = versaoAtual });

        public static ValidationProblem TituloDuplicado() => TypedResults.ValidationProblem(
            new Dictionary<string, string[]> { ["titulo"] = ["Já existe uma tarefa em aberto com este título."] });
    }
}

/// <summary>Extensão do Roteiro 4: cabeçalhos X-Total-Count e Link (RFC 8288).</summary>
public static class Paginacao
{
    public static void EscreverCabecalhos(HttpContext contexto, PaginaTarefas pagina)
    {
        var resposta = contexto.Response;
        resposta.Headers["X-Total-Count"] = pagina.TotalItens.ToString();

        var links = new List<string>();
        void Adicionar(string rel, int numero) => links.Add($"<{UrlDaPagina(contexto.Request, numero)}>; rel=\"{rel}\"");

        if (pagina.TotalPaginas > 0)
        {
            Adicionar("first", 1);
            if (pagina.Pagina > 1) Adicionar("prev", Math.Min(pagina.Pagina - 1, pagina.TotalPaginas));
            if (pagina.Pagina < pagina.TotalPaginas) Adicionar("next", pagina.Pagina + 1);
            Adicionar("last", pagina.TotalPaginas);
        }
        if (links.Count > 0)
            resposta.Headers.Link = string.Join(", ", links);
    }

    // Mantém filtros e ordenação e troca apenas o número da página.
    private static string UrlDaPagina(HttpRequest requisicao, int numero)
    {
        var parametros = requisicao.Query
            .Where(q => !q.Key.Equals("pagina", StringComparison.OrdinalIgnoreCase))
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)))
            .Append(new("pagina", numero.ToString()));
        return QueryHelpers.AddQueryString(requisicao.Path, parametros);
    }
}
