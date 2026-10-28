using System.Text.Json;

namespace PortalTarefas.Web.Infraestrutura;

/// <summary>
/// Padroniza as respostas 400 de validação: título em português e chaves de
/// <c>errors</c> em camelCase, iguais aos nomes dos campos no JSON (ex.: "titulo").
/// </summary>
public static class ProblemDetailsPadronizados
{
    public const string TituloValidacao = "Um ou mais campos são inválidos.";

    public static void AjustarValidacao(HttpValidationProblemDetails problema)
    {
        problema.Title = TituloValidacao;

        var ajustados = problema.Errors.ToDictionary(
            e => JsonNamingPolicy.CamelCase.ConvertName(e.Key),
            e => e.Value);
        problema.Errors.Clear();
        foreach (var (campo, mensagens) in ajustados)
            problema.Errors[campo] = mensagens;
    }
}
