using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PortalTarefas.Web.Dados;

namespace PortalTarefas.Web.Saude;

/// <summary>Readiness da dependência externa (serviço de calendário).</summary>
public sealed class CalendarioHealthCheck(IHttpClientFactory fabrica) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken cancelamento = default)
    {
        try
        {
            using var resposta = await fabrica.CreateClient("calendario-saude").GetAsync("saude", cancelamento);
            return resposta.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Status {(int)resposta.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Sem resposta");
        }
    }
}

/// <summary>
/// Liveness x readiness (Roteiro 7):
///  - /health/live  : o PROCESSO responde? Não consulta dependências — se o banco cair, reiniciar
///                    a aplicação não resolve, então o liveness continua 200.
///  - /health/ready : a aplicação pode RECEBER TRÁFEGO? Verifica o banco e a dependência essencial.
/// A resposta pública traz só o status de cada verificação, sem mensagens de exceção,
/// connection strings ou nomes de servidores.
/// </summary>
public static class ConfiguracaoSaude
{
    private const string TagReadiness = "ready";

    public static IServiceCollection AddVerificacoesDeSaude(this IServiceCollection servicos)
    {
        servicos.AddHealthChecks()
            .AddDbContextCheck<PortalTarefasDbContext>("banco", tags: [TagReadiness])
            .AddCheck<CalendarioHealthCheck>("calendario", tags: [TagReadiness]);
        return servicos;
    }

    public static void MapVerificacoesDeSaude(this IEndpointRouteBuilder rotas)
    {
        rotas.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,              // nenhuma verificação: só "o processo respondeu"
            ResponseWriter = EscreverResumo
        }).AllowAnonymous().DisableRateLimiting();

        rotas.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = verificacao => verificacao.Tags.Contains(TagReadiness),
            ResponseWriter = EscreverResumo
        }).AllowAnonymous().DisableRateLimiting();
    }

    private static Task EscreverResumo(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json";
        var corpo = new
        {
            status = relatorio.Status.ToString(),
            verificacoes = relatorio.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        };
        return contexto.Response.WriteAsync(JsonSerializer.Serialize(corpo, JsonSerializerOptions.Web));
    }
}
