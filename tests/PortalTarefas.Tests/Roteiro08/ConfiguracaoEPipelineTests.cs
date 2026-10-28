using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PortalTarefas.Tests.Apoio;

namespace PortalTarefas.Tests.Roteiro08;

/// <summary>Fábrica sem a connection string obrigatória.</summary>
public sealed class FabricaSemConexao : FabricaPortal
{
    protected override IEnumerable<KeyValuePair<string, string?>> ConfiguracoesAdicionais() =>
        [new("ConnectionStrings:PortalTarefas", "")];
}

public class ConfiguracaoTests
{
    [Fact]
    public void Configuracao_obrigatoria_ausente_impede_a_inicializacao_com_mensagem_segura()
    {
        using var fabrica = new FabricaSemConexao();

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());
        var validacao = erro as OptionsValidationException ?? erro.InnerException as OptionsValidationException
            ?? (erro as AggregateException)?.InnerExceptions.OfType<OptionsValidationException>().FirstOrDefault();

        Assert.NotNull(validacao);
        Assert.Contains("ConnectionStrings:PortalTarefas", validacao.Message);  // diz o que falta
        Assert.DoesNotContain("Password", validacao.Message);                   // sem valores
    }
}

public class VersaoImplantadaTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    [Fact]
    public async Task Endpoint_de_versao_informa_commit_e_ambiente()
    {
        var corpo = JsonNode.Parse(await fabrica.CriarClienteHttps().GetStringAsync("/versao"))!;

        Assert.Equal("local", corpo["commit"]!.GetValue<string>()); // na imagem: o SHA do commit
        Assert.Equal("Development", corpo["ambiente"]!.GetValue<string>());
    }
}

/// <summary>
/// Usado pelo pipeline para DEMONSTRAR o bloqueio da publicação: no GitHub Actions,
/// "Run workflow" com falha_deliberada = true define PORTALTAREFAS_FALHA_DELIBERADA e este
/// teste falha; o job de testes encerra e os jobs de imagem/implantação não executam.
/// </summary>
public class FalhaDeliberadaTests
{
    [Fact]
    public void Pipeline_sem_falha_deliberada()
    {
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORTALTAREFAS_FALHA_DELIBERADA")),
            "Falha deliberada solicitada no pipeline (workflow_dispatch falha_deliberada = true).");
    }
}
