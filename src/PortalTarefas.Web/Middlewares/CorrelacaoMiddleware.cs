using System.Text.RegularExpressions;

namespace PortalTarefas.Web.Middlewares;

/// <summary>
/// Garante que toda requisição tenha um identificador de correlação.
/// Reaproveita o valor recebido no cabeçalho (se for seguro) ou gera um novo,
/// devolve-o na resposta e o inclui no escopo de log.
/// </summary>
public sealed partial class CorrelacaoMiddleware(RequestDelegate proximo, ILogger<CorrelacaoMiddleware> logger)
{
    public const string NomeCabecalho = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext contexto)
    {
        var recebido = contexto.Request.Headers[NomeCabecalho].ToString();
        var correlacaoId = FormatoSeguro().IsMatch(recebido) ? recebido : Guid.NewGuid().ToString("N");

        contexto.TraceIdentifier = correlacaoId;
        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers[NomeCabecalho] = correlacaoId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlacaoId))
        {
            await proximo(contexto);
        }
    }

    // Aceita apenas letras, números e hífen (até 64 caracteres) para evitar injeção em logs.
    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex FormatoSeguro();
}

public static class CorrelacaoExtensions
{
    public static IApplicationBuilder UseCorrelacao(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelacaoMiddleware>();
}
