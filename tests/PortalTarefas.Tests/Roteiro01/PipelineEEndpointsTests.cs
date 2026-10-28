using System.Net;
using System.Net.Http.Json;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Modelos;

namespace PortalTarefas.Tests.Roteiro01;

/// <summary>
/// Testes de integração do Roteiro 1: sobem a aplicação em memória
/// (WebApplicationFactory) e exercitam o pipeline e os endpoints reais.
/// </summary>
public class PipelineEEndpointsTests(FabricaPortal fabrica)
    : IClassFixture<FabricaPortal>
{
    private readonly HttpClient _cliente = fabrica.CriarClienteHttps();

    [Fact]
    public async Task Obter_por_id_existente_retorna_200()
    {
        var resposta = await _cliente.GetAsync("/api/tarefas/1");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var tarefa = await resposta.Content.ReadFromJsonAsync<TarefaDto>();
        Assert.Equal(1, tarefa!.Id);
    }

    [Theory]
    [InlineData("/api/tarefas/999", HttpStatusCode.NotFound)]
    [InlineData("/api/tarefas/0", HttpStatusCode.BadRequest)]
    public async Task Obter_por_id_invalido_ou_inexistente_retorna_problem_details(string url, HttpStatusCode esperado)
    {
        var resposta = await _cliente.GetAsync(url);

        Assert.Equal(esperado, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Filtro_por_query_string_e_cabecalho_opcional()
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/tarefas?situacao=Concluida");
        requisicao.Headers.Add("X-Cliente-Id", "teste-automatizado");

        var resposta = await _cliente.SendAsync(requisicao);
        var tarefas = await resposta.Content.ReadFromJsonAsync<List<TarefaDto>>();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.NotEmpty(tarefas!);
        Assert.All(tarefas!, t => Assert.Equal(SituacaoTarefa.Concluida, t.Situacao));
    }

    [Fact]
    public async Task Post_valido_retorna_201_com_location()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/tarefas", new { titulo = "Nova tarefa de teste" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.NotNull(resposta.Headers.Location);
    }

    [Fact]
    public async Task Post_com_titulo_vazio_retorna_400_com_erros()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/tarefas", new { titulo = "" });
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("Titulo", corpo);
    }

    [Fact]
    public async Task Excecao_e_convertida_em_500_sem_detalhes_internos()
    {
        var resposta = await _cliente.GetAsync("/api/diagnostico/erro");
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.DoesNotContain("detalhe interno", corpo);
        Assert.DoesNotContain("StackTrace", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("correlationId", corpo);
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://site-malicioso.exemplo", false)]
    public async Task Cors_aceita_somente_origem_autorizada(string origem, bool deveAceitar)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Options, "/api/tarefas");
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "GET");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal(deveAceitar, resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Correlacao_recebida_e_devolvida_no_cabecalho()
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/tarefas/1");
        requisicao.Headers.Add("X-Correlation-Id", "teste-correlacao-123");

        var resposta = await _cliente.SendAsync(requisicao);

        Assert.Equal("teste-correlacao-123", resposta.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Theory]
    [InlineData("/api/diagnostico/minimal", "Minimal API")]
    [InlineData("/api/diagnostico/mvc", "MVC (controller)")]
    public async Task Endpoints_de_diagnostico_sao_equivalentes(string url, string abordagem)
    {
        var resposta = await _cliente.GetAsync(url);
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains(abordagem, corpo);
    }
}
