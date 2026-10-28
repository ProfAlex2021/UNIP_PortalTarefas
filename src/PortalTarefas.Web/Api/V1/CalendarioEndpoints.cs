using Microsoft.AspNetCore.Http.HttpResults;
using PortalTarefas.Web.Resiliencia;
using PortalTarefas.Web.Seguranca;

namespace PortalTarefas.Web.Api.V1;

/// <summary>Consulta à dependência externa através do HttpClient resiliente (Roteiro 7).</summary>
public static class CalendarioEndpoints
{
    public static IEndpointRouteBuilder MapCalendarioV1(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/api/v1/calendario/feriados/{ano:int}", ObterFeriados)
            .WithTags("Calendário")
            .WithName("ObterFeriados")
            .WithSummary("Feriados nacionais do ano, obtidos do serviço externo de calendário")
            .WithDescription("Usa timeout, retry para falhas transitórias e circuit breaker. " +
                             "Se a dependência estiver indisponível, responde 503.")
            .RequireAuthorization(Politicas.LerTarefas)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return rotas;
    }

    private static async Task<Results<Ok<IReadOnlyList<Feriado>>, ProblemHttpResult>> ObterFeriados(
        int ano, ClienteCalendario calendario, ILogger<ClienteCalendario> logger, CancellationToken cancelamento)
    {
        try
        {
            return TypedResults.Ok(await calendario.ObterFeriadosAsync(ano, cancelamento));
        }
        catch (Exception ex) when (ConfiguracaoClienteCalendario.IndicaIndisponibilidade(ex))
        {
            // Registra o TIPO da falha (transitória esgotada, circuito aberto, timeout) — útil no diagnóstico.
            logger.LogWarning("Serviço de calendário indisponível: {TipoFalha}", ex.GetType().Name);
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Serviço de calendário indisponível.",
                detail: "Tente novamente em alguns instantes.");
        }
    }
}
