using System.ComponentModel;
using Microsoft.AspNetCore.Http.HttpResults;
using PortalTarefas.Web.Capacidade;
using PortalTarefas.Web.Servicos.Painel;

namespace PortalTarefas.Web.Api;

[Description("Resumo público e agregado das tarefas em aberto (sem dados pessoais).")]
public sealed record ResumoPublico(IReadOnlyList<LinhaPainel> Linhas, DateTimeOffset GeradoEm);

/// <summary>
/// Leitura PÚBLICA e NÃO PERSONALIZADA — a única adequada para output caching (Roteiro 7).
/// A mesma resposta serve qualquer visitante, por 30 s ou até alguma tarefa mudar (invalidação por tag).
/// </summary>
public static class PublicoEndpoints
{
    public static IEndpointRouteBuilder MapPublico(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet("/api/publico/painel", ObterResumo)
            .WithTags("Público")
            .WithName("ObterResumoPublico")
            .WithSummary("Resumo agregado das tarefas em aberto (cache de 30 s)")
            .WithDescription("Resposta guardada em output cache. 'geradoEm' mostra quando foi calculada; " +
                             "respostas servidas do cache trazem o cabeçalho Age.")
            .AllowAnonymous()
            .CacheOutput(ConfiguracaoCapacidade.PoliticaCachePainelPublico);
        return rotas;
    }

    private static async Task<Ok<ResumoPublico>> ObterResumo(
        IEnumerable<IConsultaPainel> consultas, TimeProvider relogio, ILogger<ResumoPublico> logger, CancellationToken cancelamento)
    {
        // Só é executado quando a resposta NÃO está no cache — o log permite distinguir.
        logger.LogInformation("Resumo público calculado (cache vazio ou expirado)");
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        var linhas = await consultas.First(c => c.Fonte == "EF Core").ObterAsync(hoje, cancelamento);
        return TypedResults.Ok(new ResumoPublico(linhas, relogio.GetUtcNow()));
    }
}
