using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PortalTarefas.Web.Filtros;

/// <summary>
/// Filtro de ação que registra o nome da ação, a duração e o resultado produzido.
/// Não altera o fluxo nem substitui a validação: apenas observa. Por isso a
/// verificação de ModelState continua dentro das ações do controller.
/// </summary>
public sealed class RegistrarExecucaoAcaoFilter(ILogger<RegistrarExecucaoAcaoFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate proximo)
    {
        var rota = contexto.ActionDescriptor.RouteValues;
        var acao = $"{rota["controller"]}.{rota["action"]} [{contexto.HttpContext.Request.Method}]";
        var cronometro = Stopwatch.StartNew();
        logger.LogInformation("Ação {Acao} iniciada (ModelState válido: {ModelStateValido})",
            acao, contexto.ModelState.IsValid);

        var executada = await proximo();
        cronometro.Stop();

        if (executada.Exception is not null && !executada.ExceptionHandled)
        {
            logger.LogWarning("Ação {Acao} terminou com exceção {TipoExcecao} em {DuracaoMs:0.0} ms",
                acao, executada.Exception.GetType().Name, cronometro.Elapsed.TotalMilliseconds);
            return;
        }

        logger.LogInformation("Ação {Acao} concluída com {Resultado} (status {Status}) em {DuracaoMs:0.0} ms",
            acao, DescreverResultado(executada.Result), ObterStatus(executada.Result), cronometro.Elapsed.TotalMilliseconds);
    }

    private static string DescreverResultado(IActionResult? resultado) => resultado switch
    {
        ViewResult v => $"View '{v.ViewName ?? "(padrão)"}'",
        RedirectToActionResult r => $"Redirect para {r.ActionName}",
        null => "nenhum resultado",
        _ => resultado.GetType().Name
    };

    private static int ObterStatus(IActionResult? resultado) => resultado switch
    {
        IStatusCodeActionResult { StatusCode: { } status } => status,
        RedirectToActionResult => StatusCodes.Status302Found,
        _ => StatusCodes.Status200OK
    };
}
