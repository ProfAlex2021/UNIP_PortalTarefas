using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PortalTarefas.Web.Infraestrutura;

/// <summary>
/// Converte exceções não tratadas em Problem Details 500 sem expor mensagem,
/// stack trace ou detalhes internos. O cliente recebe apenas o identificador
/// de correlação para localizar o erro nos logs.
/// </summary>
public sealed class TratadorExcecoesGlobal(
    IProblemDetailsService problemDetailsService,
    ILogger<TratadorExcecoesGlobal> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken cancelamento)
    {
        logger.LogError(excecao, "Erro não tratado ao processar {Metodo} {Caminho} (correlação {CorrelationId})",
            contexto.Request.Method, contexto.Request.Path.Value, contexto.TraceIdentifier);

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excecao,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno ao processar a requisição.",
                Detail = "Informe o identificador de correlação ao suporte.",
                Extensions = { ["correlationId"] = contexto.TraceIdentifier }
            }
        });
    }
}
