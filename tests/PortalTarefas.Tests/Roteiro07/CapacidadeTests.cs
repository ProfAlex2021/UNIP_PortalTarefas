using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Api.V1;

namespace PortalTarefas.Tests.Roteiro07;

/// <summary>Fábrica com limite baixo de login, para provocar o 429 rapidamente.</summary>
public sealed class FabricaLimiteBaixo : FabricaPortal
{
    protected override IEnumerable<KeyValuePair<string, string?>> ConfiguracoesAdicionais() =>
        [new("Limites:Login:PermitidasPorJanela", "3"), new("Limites:Login:JanelaSegundos", "60")];
}

/// <summary>Extensão do Roteiro 7: teste automatizado da política de rate limiting.</summary>
public class RateLimitingTests(FabricaLimiteBaixo fabrica) : IClassFixture<FabricaLimiteBaixo>
{
    [Fact]
    public async Task Excesso_de_tentativas_de_login_recebe_429_com_retry_after()
    {
        var cliente = fabrica.CriarClienteHttps();
        var credenciais = new { email = "qualquer@portaltarefas.local", senha = "SenhaErrada123" };

        var statusPermitidas = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statusPermitidas.Add((await cliente.PostAsJsonAsync("/api/auth/login", credenciais)).StatusCode);
        var excedente = await cliente.PostAsJsonAsync("/api/auth/login", credenciais);

        Assert.All(statusPermitidas, s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
        Assert.True(excedente.Headers.Contains("Retry-After"));
        Assert.Contains("Muitas requisições", await excedente.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_check_nao_e_afetado_pelo_limite()
    {
        var cliente = fabrica.CriarClienteHttps();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live")).StatusCode);
    }
}

/// <summary>Output caching em leitura pública e não personalizada, com invalidação por tag.</summary>
public class OutputCacheTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    private const string Url = "/api/publico/painel";

    private static async Task<string> GeradoEm(HttpResponseMessage resposta) =>
        JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!["geradoEm"]!.GetValue<string>();

    [Fact]
    public async Task Segunda_leitura_anonima_vem_do_cache_e_alteracao_invalida()
    {
        var anonimo = fabrica.CriarClienteHttps();

        var primeira = await anonimo.GetAsync(Url);
        var segunda = await anonimo.GetAsync(Url);

        Assert.Equal(await GeradoEm(primeira), await GeradoEm(segunda)); // mesma resposta calculada uma vez
        Assert.True(segunda.Headers.Age.HasValue);                       // servida do cache

        // Criar uma tarefa invalida a tag "tarefas": a próxima leitura é recalculada.
        var admin = await fabrica.CriarClienteAutenticadoAsync();
        (await admin.PostAsJsonAsync("/api/v1/tarefas", new TarefaCriacao($"Invalida cache {Guid.NewGuid():N}"), Json.Opcoes))
            .EnsureSuccessStatusCode();

        var terceira = await anonimo.GetAsync(Url);
        Assert.NotEqual(await GeradoEm(segunda), await GeradoEm(terceira));
    }

    [Fact]
    public async Task Requisicao_autenticada_nao_e_servida_do_cache()
    {
        var anonimo = fabrica.CriarClienteHttps();
        await anonimo.GetAsync(Url); // popula o cache

        var autenticado = await fabrica.CriarClienteAutenticadoAsync();
        var resposta = await autenticado.GetAsync(Url);

        Assert.False(resposta.Headers.Age.HasValue);
    }
}
