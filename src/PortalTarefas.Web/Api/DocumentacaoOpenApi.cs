using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PortalTarefas.Web.Api;

/// <summary>Ajustes no documento OpenAPI gerado por Microsoft.AspNetCore.OpenApi.</summary>
public static class DocumentacaoOpenApi
{
    public const string NomeDocumento = "v1";

    public static IServiceCollection AddDocumentacaoOpenApi(this IServiceCollection servicos) =>
        servicos.AddOpenApi(NomeDocumento, opcoes =>
        {
            opcoes.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info = new OpenApiInfo
                {
                    Title = "Portal de Tarefas — API",
                    Version = "v1",
                    Description = "API REST do Portal de Tarefas (Desenvolvimento Web com .NET — UNIP). " +
                                  "Erros seguem Problem Details (RFC 9457). Enums são trafegados como texto."
                };
                return Task.CompletedTask;
            });
        });

    /// <summary>
    /// Documenta os cabeçalhos de paginação da listagem (X-Total-Count e Link),
    /// que o gerador não descobre sozinho porque são escritos no HttpContext.
    /// </summary>
    public static Task DocumentarCabecalhosDePaginacao(
        OpenApiOperation operacao, OpenApiOperationTransformerContext contexto, CancellationToken cancelamento)
    {
        if (operacao.Responses?.TryGetValue("200", out var resposta) == true && resposta is OpenApiResponse ok)
        {
            ok.Headers ??= new Dictionary<string, IOpenApiHeader>();
            ok.Headers["X-Total-Count"] = new OpenApiHeader
            {
                Description = "Total de itens que atendem aos filtros.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer }
            };
            ok.Headers["Link"] = new OpenApiHeader
            {
                Description = "Links de navegação (RFC 8288) com rel first, prev, next e last.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            };
        }
        return Task.CompletedTask;
    }
}
