using System.Text.Json.Nodes;
using PortalTarefas.Tests.Apoio;

namespace PortalTarefas.Tests.Roteiro04;

/// <summary>
/// Extensão do Roteiro 4: testes de contrato. Garantem que o documento servido pela
/// aplicação é o mesmo versionado em contratos/ (snapshot) e que respostas e esquemas
/// essenciais continuam descritos. Uma mudança no contrato exige atualizar o arquivo
/// versionado (o build faz isso) e regenerar o cliente TypeScript.
/// </summary>
public class ContratoOpenApiTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    private readonly HttpClient _cliente = fabrica.CriarClienteHttps();

    private async Task<JsonObject> DocumentoEmExecucao()
    {
        var documento = JsonNode.Parse(await _cliente.GetStringAsync("/openapi/v1.json"))!.AsObject();
        documento.Remove("servers"); // depende do host em que a aplicação está rodando
        return documento;
    }

    private static JsonObject DocumentoVersionado()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "PortalTarefas.slnx")))
            pasta = pasta.Parent;
        var caminho = Path.Combine(pasta!.FullName, "contratos", "PortalTarefas.Web.json");
        var documento = JsonNode.Parse(File.ReadAllText(caminho))!.AsObject();
        documento.Remove("servers");
        return documento;
    }

    [Fact]
    public async Task Documento_em_execucao_e_igual_ao_snapshot_versionado()
    {
        var emExecucao = await DocumentoEmExecucao();
        var versionado = DocumentoVersionado();

        Assert.True(JsonNode.DeepEquals(versionado, emExecucao),
            "O contrato mudou. Compile o projeto Web para atualizar contratos/PortalTarefas.Web.json, " +
            "revise a diferença e regenere o cliente TypeScript (npm run gerar-cliente).");
    }

    [Theory]
    [InlineData("/api/v1/tarefas", "get", "200 400")]
    [InlineData("/api/v1/tarefas", "post", "201 400")]
    [InlineData("/api/v1/tarefas/{id}", "get", "200 404")]
    [InlineData("/api/v1/tarefas/{id}", "put", "200 400 404 409")]
    [InlineData("/api/v1/tarefas/{id}", "delete", "204 404")]
    public async Task Operacoes_documentam_as_respostas_do_contrato(string caminho, string metodo, string codigos)
    {
        var respostas = (await DocumentoEmExecucao())["paths"]![caminho]![metodo]!["responses"]!.AsObject();

        foreach (var codigo in codigos.Split(' '))
            Assert.True(respostas.ContainsKey(codigo), $"{metodo.ToUpper()} {caminho} não documenta {codigo}");
    }

    [Fact]
    public async Task Esquemas_expoem_apenas_dtos_e_nao_a_entidade()
    {
        var esquemas = (await DocumentoEmExecucao())["components"]!["schemas"]!.AsObject();

        Assert.Contains("TarefaDetalhe", esquemas.Select(e => e.Key));
        Assert.DoesNotContain("Tarefa", esquemas.Select(e => e.Key));
        Assert.DoesNotContain("EventoAuditoria", esquemas.Select(e => e.Key));
    }
}
