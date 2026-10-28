using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Observabilidade;

/// <summary>
/// Métricas e spans próprios da aplicação (Roteiro 7).
/// As dimensões (tags) têm cardinalidade controlada: valores de enum ou um conjunto fixo,
/// nunca ids, títulos ou e-mails — isso explodiria o número de séries temporais.
/// </summary>
public sealed class Telemetria : IDisposable
{
    public const string Nome = "PortalTarefas";

    /// <summary>Fonte dos spans customizados (tracing).</summary>
    public static readonly ActivitySource Atividades = new(Nome);

    private readonly Meter _medidor;
    private readonly Counter<long> _tarefasCriadas;
    private readonly Counter<long> _conflitos;
    private readonly Histogram<double> _duracaoPainel;

    public Telemetria(IMeterFactory fabrica)
    {
        _medidor = fabrica.Create(Nome);

        // Métrica de NEGÓCIO: quantas tarefas são criadas, por prioridade (3 valores possíveis).
        _tarefasCriadas = _medidor.CreateCounter<long>("portaltarefas.tarefas.criadas", unit: "{tarefa}",
            description: "Tarefas cadastradas.");

        // Métrica de negócio/técnica: conflitos de concorrência detectados na edição.
        _conflitos = _medidor.CreateCounter<long>("portaltarefas.tarefas.conflitos", unit: "{conflito}",
            description: "Edições recusadas por conflito de concorrência.");

        // Métrica TÉCNICA: duração da consulta de painel, por fonte (ef | dapper).
        _duracaoPainel = _medidor.CreateHistogram<double>("portaltarefas.painel.duracao", unit: "ms",
            description: "Duração da consulta do painel.");
    }

    public void TarefaCriada(PrioridadeTarefa prioridade) =>
        _tarefasCriadas.Add(1, new KeyValuePair<string, object?>("prioridade", prioridade.ToString()));

    public void ConflitoDetectado() => _conflitos.Add(1);

    public void PainelConsultado(string fonte, double milissegundos) =>
        _duracaoPainel.Record(milissegundos, new KeyValuePair<string, object?>("fonte", fonte));

    public void Dispose() => _medidor.Dispose();
}

public static class ConfiguracaoTelemetria
{
    /// <summary>
    /// OpenTelemetry para ASP.NET Core, HttpClient e SqlClient, mais os sinais próprios.
    /// Exportação:
    ///  - Telemetria:ExportarConsole = true  -> spans e métricas no console (evidência rápida);
    ///  - OTEL_EXPORTER_OTLP_ENDPOINT definido -> envia logs, métricas e traces para um coletor
    ///    local (ex.: Aspire Dashboard em docker-compose.observabilidade.yml).
    /// </summary>
    public static WebApplicationBuilder AddObservabilidade(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Telemetria>();

        var exportarConsole = builder.Configuration.GetValue<bool>("Telemetria:ExportarConsole");
        var usarOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Logging.AddOpenTelemetry(opcoes =>
        {
            opcoes.IncludeScopes = true;           // CorrelationId e TarefaId viajam com o log
            opcoes.IncludeFormattedMessage = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: "PortalTarefas.Web",
                serviceVersion: typeof(Telemetria).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(Telemetria.Nome)
                    .AddAspNetCoreInstrumentation(o =>
                        // Health checks são chamados o tempo todo por orquestradores: não poluem os traces.
                        o.Filter = contexto => !contexto.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(); // texto do comando não é gravado por padrão

                if (exportarConsole) tracing.AddConsoleExporter();
            })
            .WithMetrics(metricas =>
            {
                metricas
                    .AddMeter(Telemetria.Nome)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Microsoft.AspNetCore.RateLimiting", "Microsoft.AspNetCore.OutputCaching")
                    .AddMeter("Polly"); // eventos de retry e circuit breaker

                if (exportarConsole) metricas.AddConsoleExporter();
            });

        if (usarOtlp)
            otel.UseOtlpExporter(); // lê OTEL_EXPORTER_OTLP_ENDPOINT / OTEL_EXPORTER_OTLP_PROTOCOL

        return builder;
    }
}
