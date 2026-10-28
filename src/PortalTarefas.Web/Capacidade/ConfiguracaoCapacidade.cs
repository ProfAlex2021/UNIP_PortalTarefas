using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace PortalTarefas.Web.Capacidade;

/// <summary>
/// Rate limiting e output caching (Roteiro 7). São mecanismos distintos:
///  - rate limiting PROTEGE (limita quantas requisições um cliente pode fazer);
///  - output caching ACELERA (reaproveita uma resposta pública já calculada).
/// </summary>
public static class ConfiguracaoCapacidade
{
    public const string PoliticaLogin = "login";
    public const string PoliticaCachePainelPublico = "painel-publico";
    public const string TagCacheTarefas = "tarefas";

    public static IServiceCollection AddControlesDeCapacidade(this IServiceCollection servicos)
    {
        servicos.AddRateLimiter(opcoes =>
        {
            // Login: no máximo N tentativas por janela, por endereço IP. Dificulta força bruta
            // (complementa o bloqueio de conta do Identity, que é por usuário).
            // Os valores vêm de configuração (Limites:Login:*), lida na primeira requisição de cada IP.
            opcoes.AddPolicy(PoliticaLogin, contexto => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ =>
                {
                    var configuracao = contexto.RequestServices.GetRequiredService<IConfiguration>();
                    return new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuracao.GetValue("Limites:Login:PermitidasPorJanela", 5),
                        Window = TimeSpan.FromSeconds(configuracao.GetValue("Limites:Login:JanelaSegundos", 60)),
                        QueueLimit = 0
                    };
                }));

            // Resposta para o excesso: 429 + Retry-After + Problem Details.
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.OnRejected = async (contexto, cancelamento) =>
            {
                var http = contexto.HttpContext;
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var esperar))
                    http.Response.Headers.RetryAfter = ((int)esperar.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Capacidade")
                    .LogWarning("Limite de requisições excedido em {Caminho}", http.Request.Path.Value);

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas requisições.",
                        Detail = "Limite de tentativas excedido. Aguarde antes de tentar novamente."
                    }
                });
            };
        });

        servicos.AddOutputCache(opcoes =>
        {
            // Somente para leitura PÚBLICA e não personalizada (o resumo agregado do painel).
            // A política padrão já não guarda respostas de usuários autenticados nem com Set-Cookie.
            opcoes.AddPolicy(PoliticaCachePainelPublico, politica => politica
                .Expire(TimeSpan.FromSeconds(30))
                .Tag(TagCacheTarefas));
        });

        return servicos;
    }
}
