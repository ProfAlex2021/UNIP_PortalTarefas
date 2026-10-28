using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Resiliencia;

namespace PortalTarefas.Tests.Roteiro07;

/// <summary>Liveness x readiness com dependência saudável e indisponível.</summary>
public class SaudeTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    private readonly HttpClient _cliente = fabrica.CriarClienteHttps();

    private Task DefinirModo(string modo) => _cliente.PostAsync($"/simulacao/calendario/modo?modo={modo}", null);

    [Fact]
    public async Task Dependencia_indisponivel_afeta_readiness_mas_nao_liveness()
    {
        await DefinirModo("Normal");
        var prontoAntes = await _cliente.GetAsync("/health/ready");

        await DefinirModo("Indisponivel");
        var vivo = await _cliente.GetAsync("/health/live");
        var pronto = await _cliente.GetAsync("/health/ready");
        var corpo = JsonNode.Parse(await pronto.Content.ReadAsStringAsync())!;
        await DefinirModo("Normal");

        Assert.Equal(HttpStatusCode.OK, prontoAntes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vivo.StatusCode);                    // o processo responde
        Assert.Equal(HttpStatusCode.ServiceUnavailable, pronto.StatusCode);  // mas não deve receber tráfego
        Assert.Equal("Unhealthy", corpo["verificacoes"]!["calendario"]!.GetValue<string>());
        Assert.Equal("Healthy", corpo["verificacoes"]!["banco"]!.GetValue<string>());
    }

    [Fact]
    public async Task Resposta_publica_nao_expoe_detalhes_sensiveis()
    {
        var corpo = await _cliente.GetStringAsync("/health/ready");

        Assert.DoesNotContain("Server=", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localdb", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", corpo, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Retry e circuit breaker de ponta a ponta, pela API, contra a dependência simulada.</summary>
public class ResilienciaPelaApiTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    [Fact]
    public async Task Falha_transitoria_e_absorvida_pelo_retry()
    {
        var cliente = await fabrica.CriarClienteAutenticadoAsync();
        await cliente.PostAsync("/simulacao/calendario/modo?modo=FalhaTransitoria&falhas=2", null);

        var resposta = await cliente.GetAsync("/api/v1/calendario/feriados/2026");
        var estado = fabrica.Services.GetRequiredService<EstadoSimulacaoCalendario>();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(3, estado.ChamadasRecebidas); // 2 falhas + 1 sucesso
        Assert.NotEmpty((await resposta.Content.ReadFromJsonAsync<List<Feriado>>(Json.Opcoes))!);
    }
}

/// <summary>
/// Testes do pipeline de resiliência isolado, com um handler falso no lugar da rede.
/// Usa exatamente a mesma configuração registrada pela aplicação (AddClienteCalendario).
/// </summary>
public class PipelineResilienciaTests
{
    private sealed class HandlerFalso(Func<int, HttpStatusCode> respostaPorChamada) : HttpMessageHandler
    {
        public int Chamadas;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken cancelamento)
        {
            var status = respostaPorChamada(Interlocked.Increment(ref Chamadas));
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = requisicao,
                Content = status == HttpStatusCode.OK ? JsonContent.Create(Array.Empty<Feriado>()) : null
            });
        }
    }

    private static (ClienteCalendario Cliente, HandlerFalso Handler) Criar(Func<int, HttpStatusCode> respostas)
    {
        var handler = new HandlerFalso(respostas);
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Dependencias:Calendario:UrlBase"] = "https://calendario.teste/" }).Build());
        servicos.AddClienteCalendario().ConfigurePrimaryHttpMessageHandler(() => handler);
        return (servicos.BuildServiceProvider().GetRequiredService<ClienteCalendario>(), handler);
    }

    [Fact]
    public async Task Retry_somente_em_falha_transitoria()
    {
        var (cliente, handler) = Criar(n => n <= 2 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);

        await cliente.ObterFeriadosAsync(2026, CancellationToken.None);

        Assert.Equal(3, handler.Chamadas);
    }

    [Fact]
    public async Task Erro_nao_transitorio_nao_e_repetido()
    {
        var (cliente, handler) = Criar(_ => HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.ObterFeriadosAsync(2026, CancellationToken.None));

        Assert.Equal(1, handler.Chamadas); // 400 é erro do cliente: repetir não adianta
    }

    [Fact]
    public async Task Circuit_breaker_abre_e_passa_a_falhar_sem_chamar_a_dependencia()
    {
        var (cliente, handler) = Criar(_ => HttpStatusCode.ServiceUnavailable);

        // 1ª operação: 1 tentativa + 3 retries, todas com 503 -> atinge o mínimo de 4 chamadas com 100% de falha.
        await Assert.ThrowsAnyAsync<Exception>(() => cliente.ObterFeriadosAsync(2026, CancellationToken.None));
        var chamadasAteAbrir = handler.Chamadas;

        // Com o circuito aberto, a falha é imediata e a dependência não é chamada.
        await Assert.ThrowsAsync<BrokenCircuitException>(() => cliente.ObterFeriadosAsync(2026, CancellationToken.None));
        Assert.Equal(chamadasAteAbrir, handler.Chamadas);
    }
}
