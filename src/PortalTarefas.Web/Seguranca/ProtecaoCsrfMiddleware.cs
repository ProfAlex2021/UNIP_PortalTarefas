namespace PortalTarefas.Web.Seguranca;

/// <summary>
/// Proteção CSRF para a API autenticada por cookie.
/// Além do cookie SameSite=Strict, toda requisição que ALTERA dados em /api e carrega o
/// cookie de autenticação precisa do cabeçalho personalizado X-Requested-With.
/// Um site de outra origem não consegue enviar esse cabeçalho sem um preflight CORS,
/// e a política CORS só aceita a origem do front-end. Formulários MVC usam o token
/// antiforgery tradicional (AutoValidateAntiforgeryToken).
/// </summary>
public sealed class ProtecaoCsrfMiddleware(RequestDelegate proximo)
{
    public const string Cabecalho = "X-Requested-With";
    public const string ValorEsperado = "PortalTarefas";

    public async Task InvokeAsync(HttpContext contexto, IProblemDetailsService problemas)
    {
        var requisicao = contexto.Request;
        var alteraDados = !(HttpMethods.IsGet(requisicao.Method) || HttpMethods.IsHead(requisicao.Method) || HttpMethods.IsOptions(requisicao.Method));
        var usaCookieDeSessao = requisicao.Cookies.ContainsKey(ConfiguracaoSeguranca.NomeCookie);

        if (alteraDados && usaCookieDeSessao && requisicao.Path.StartsWithSegments("/api") &&
            requisicao.Headers[Cabecalho] != ValorEsperado)
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemas.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = contexto,
                ProblemDetails =
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Requisição recusada pela proteção CSRF.",
                    Detail = $"Envie o cabeçalho {Cabecalho}: {ValorEsperado}."
                }
            });
            return;
        }

        await proximo(contexto);
    }
}
