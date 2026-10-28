using System.Net.Http.Json;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;

namespace PortalTarefas.Web.Resiliencia;

/// <summary>Feriado devolvido pelo serviço externo de calendário.</summary>
public sealed record Feriado(DateOnly Data, string Nome);

/// <summary>
/// Cliente tipado de uma dependência HTTP externa (serviço de calendário de feriados).
/// Em desenvolvimento a dependência é simulada pela própria aplicação em /simulacao/calendario,
/// o que permite provocar falhas transitórias, lentidão e indisponibilidade de forma reproduzível.
/// </summary>
public sealed class ClienteCalendario(HttpClient http)
{
    public const string NomePipeline = "calendario";

    /// <summary>GET é seguro e idempotente: pode ser repetido pela política de retry.</summary>
    public async Task<IReadOnlyList<Feriado>> ObterFeriadosAsync(int ano, CancellationToken cancelamento) =>
        await http.GetFromJsonAsync<List<Feriado>>($"feriados/{ano}", cancelamento) ?? [];
}

public static class ConfiguracaoClienteCalendario
{
    public static IHttpClientBuilder AddClienteCalendario(this IServiceCollection servicos)
    {
        // A URL é lida quando o cliente é criado (e não no registro), para poder ser trocada por ambiente e em testes.
        static Uri UrlBase(IServiceProvider sp) => new(
            sp.GetRequiredService<IConfiguration>()["Dependencias:Calendario:UrlBase"] ?? "https://localhost:7260/simulacao/calendario/");

        // Cliente simples (sem retry) usado pelo health check de readiness: queremos saber
        // rapidamente se a dependência está de pé, não insistir até ela responder.
        servicos.AddHttpClient("calendario-saude", (sp, c) =>
        {
            c.BaseAddress = UrlBase(sp);
            c.Timeout = TimeSpan.FromSeconds(2);
        });

        var cliente = servicos.AddHttpClient<ClienteCalendario>((sp, c) => c.BaseAddress = UrlBase(sp));

        // Ordem do pipeline (de fora para dentro):
        //   timeout total -> retry -> circuit breaker -> timeout por tentativa -> requisição
        cliente.AddResilienceHandler(ClienteCalendario.NomePipeline, pipeline =>
        {
            pipeline.AddTimeout(TimeSpan.FromSeconds(10)); // limite para a operação inteira

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,                          // tentativas FINITAS
                BackoffType = DelayBackoffType.Exponential,    // 200 ms, 400 ms, 800 ms (+ jitter)
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
                // Só repete falhas transitórias (5xx, 408, 429, erro de rede, timeout da tentativa)
                // e somente em métodos idempotentes. Um POST não é repetido automaticamente.
                ShouldHandle = argumentos =>
                {
                    var metodo = argumentos.Outcome.Result?.RequestMessage?.Method;
                    var idempotente = metodo is null || metodo == HttpMethod.Get || metodo == HttpMethod.Head;
                    return ValueTask.FromResult(idempotente && HttpClientResiliencePredicates.IsTransient(argumentos.Outcome));
                }
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,                            // abre com 50% de falhas...
                MinimumThroughput = 4,                         // ...em pelo menos 4 chamadas...
                SamplingDuration = TimeSpan.FromSeconds(30),   // ...na janela de 30 s
                BreakDuration = TimeSpan.FromSeconds(15)       // e fica aberto 15 s (falha rápida)
            });

            pipeline.AddTimeout(TimeSpan.FromSeconds(2));      // limite por tentativa
        });

        return cliente;
    }

    /// <summary>Exceções que indicam dependência indisponível (para responder 503).</summary>
    public static bool IndicaIndisponibilidade(Exception ex) =>
        ex is HttpRequestException or BrokenCircuitException or Polly.Timeout.TimeoutRejectedException;
}
