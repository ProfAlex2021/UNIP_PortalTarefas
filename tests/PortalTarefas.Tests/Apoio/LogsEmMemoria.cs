using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PortalTarefas.Tests.Apoio;

/// <summary>Registro de log capturado, com a mensagem e os escopos (ex.: CorrelationId).</summary>
public sealed record LogCapturado(LogLevel Nivel, string Categoria, string Mensagem, string Escopos)
{
    public string TextoCompleto => $"{Mensagem} | {Escopos}";
}

/// <summary>Provedor de log que guarda tudo em memória, para os testes inspecionarem.</summary>
public sealed class LogsEmMemoria : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? _escopos;
    public ConcurrentQueue<LogCapturado> Registros { get; } = new();

    public ILogger CreateLogger(string categoria) => new Registrador(this, categoria);
    public void SetScopeProvider(IExternalScopeProvider escopos) => _escopos = escopos;
    public void Dispose() { }

    private sealed class Registrador(LogsEmMemoria dono, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            dono._escopos?.Push(state);

        public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Information;

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? excecao, Func<TState, Exception?, string> formatar)
        {
            var escopos = new List<string>();
            dono._escopos?.ForEachScope((escopo, lista) => lista.Add(escopo?.ToString() ?? ""), escopos);
            dono.Registros.Enqueue(new LogCapturado(nivel, categoria, formatar(estado, excecao), string.Join(" ; ", escopos)));
        }
    }
}
