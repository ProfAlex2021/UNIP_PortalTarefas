using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Endpoints;

namespace PortalTarefas.Web.Controllers;

/// <summary>
/// Versão MVC (controller) do endpoint de diagnóstico, equivalente a
/// GET /api/diagnostico/minimal. Serve para comparar as duas abordagens.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)] // endpoint de comparação do Roteiro 1, fora do OpenAPI
[Route("api/diagnostico")]
public sealed class DiagnosticoController(IWebHostEnvironment ambiente) : ControllerBase
{
    [HttpGet("mvc")]
    [ProducesResponseType<DiagnosticoResposta>(StatusCodes.Status200OK)]
    public ActionResult<DiagnosticoResposta> Obter() => Ok(new DiagnosticoResposta(
        "MVC (controller)",
        ambiente.EnvironmentName,
        Environment.Version.ToString(),
        DateTimeOffset.UtcNow));
}
