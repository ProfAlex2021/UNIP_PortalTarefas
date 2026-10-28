namespace PortalTarefas.Web.Endpoints;

/// <summary>Resposta compartilhada pelas duas versões do endpoint de diagnóstico.</summary>
public sealed record DiagnosticoResposta(string Abordagem, string Ambiente, string VersaoRuntime, DateTimeOffset GeradoEm);

/// <summary>
/// Versão Minimal API do endpoint de diagnóstico. A versão MVC equivalente está em
/// Controllers/DiagnosticoController.cs.
/// </summary>
public static class DiagnosticoEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticoEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/diagnostico").WithTags("Diagnóstico");

        grupo.MapGet("/minimal", (IWebHostEnvironment ambiente) => TypedResults.Ok(new DiagnosticoResposta(
            "Minimal API",
            ambiente.EnvironmentName,
            Environment.Version.ToString(),
            DateTimeOffset.UtcNow)));

        // Endpoint que lança uma exceção de propósito para demonstrar o tratamento centralizado.
        // Mapeado somente em desenvolvimento.
        if (rotas.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            grupo.MapGet("/erro", string () =>
                throw new InvalidOperationException("Falha simulada: detalhe interno que não deve chegar ao cliente."));
        }

        return rotas;
    }
}
