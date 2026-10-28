using System.Diagnostics;

namespace PortalTarefas.Web.Middlewares;

/// <summary>
/// Registra entrada e saída de cada requisição: método, caminho, status e duração.
/// Não registra query string, cabeçalhos nem corpo, pois podem conter credenciais
/// ou dados pessoais.
/// </summary>
public sealed class RegistroRequisicaoMiddleware(RequestDelegate proximo, ILogger<RegistroRequisicaoMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var metodo = contexto.Request.Method;
        var caminho = contexto.Request.Path.Value;
        var cronometro = Stopwatch.StartNew();

        // Roteiro 7: a entrada fica em Debug (útil só para depurar a ordem do pipeline);
        // a saída, com status e duração, é o registro principal em Information.
        logger.LogDebug("--> Entrada {Metodo} {Caminho}", metodo, caminho);
        try
        {
            await proximo(contexto);
            var status = contexto.Response.StatusCode;
            logger.Log(status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information,
                "<-- Saída {Metodo} {Caminho} respondeu {Status} em {DuracaoMs:0.0} ms",
                metodo, caminho, status, cronometro.Elapsed.TotalMilliseconds);
        }
        catch
        {
            // A exceção segue para o UseExceptionHandler (registrado antes), que gera o 500.
            logger.LogWarning(
                "<-- Saída {Metodo} {Caminho} interrompida por exceção após {DuracaoMs:0.0} ms",
                metodo, caminho, cronometro.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}

public static class RegistroRequisicaoExtensions
{
    /// <summary>Adiciona o <see cref="RegistroRequisicaoMiddleware"/> ao pipeline.</summary>
    public static IApplicationBuilder UseRegistroRequisicao(this IApplicationBuilder app)
        => app.UseMiddleware<RegistroRequisicaoMiddleware>();
}
