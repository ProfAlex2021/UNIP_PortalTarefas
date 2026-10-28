using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortalTarefas.Tests.Apoio;

/// <summary>Mesmas opções de JSON da API (camelCase + enums como texto).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
