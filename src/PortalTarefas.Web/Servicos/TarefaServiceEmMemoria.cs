using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>
/// Implementação em memória (singleton). Os dados se perdem ao reiniciar a aplicação;
/// a persistência real chega no Roteiro 3. Usa lock porque várias requisições
/// podem acessar a lista ao mesmo tempo.
/// </summary>
public sealed class TarefaServiceEmMemoria : ITarefaService
{
    private readonly Lock _trava = new();
    private readonly List<Tarefa> _tarefas;
    private int _ultimoId;

    public TarefaServiceEmMemoria(TimeProvider relogio)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        var agora = relogio.GetUtcNow();
        _tarefas =
        [
            new() { Id = 1, Titulo = "Configurar ambiente .NET 10", Descricao = "Instalar SDK e Visual Studio 2026",
                    Situacao = SituacaoTarefa.Concluida, Prioridade = PrioridadeTarefa.Alta, Prazo = hoje.AddDays(-10), CriadaEm = agora },
            new() { Id = 2, Titulo = "Estudar pipeline HTTP", Descricao = "Ler a Unidade I",
                    Situacao = SituacaoTarefa.EmAndamento, Prioridade = PrioridadeTarefa.Media, Prazo = hoje.AddDays(5), CriadaEm = agora },
            new() { Id = 3, Titulo = "Entregar relatório do Roteiro 1",
                    Situacao = SituacaoTarefa.Pendente, Prioridade = PrioridadeTarefa.Alta, Prazo = hoje.AddDays(-2), CriadaEm = agora },
        ];
        _ultimoId = _tarefas.Max(t => t.Id);
    }

    public Task<IReadOnlyList<Tarefa>> ListarAsync(FiltroTarefas filtro, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var consulta = _tarefas.AsEnumerable();
            if (filtro.Situacao is not null)
                consulta = consulta.Where(t => t.Situacao == filtro.Situacao);
            if (!string.IsNullOrWhiteSpace(filtro.Busca))
                consulta = consulta.Where(t => t.Titulo.Contains(filtro.Busca, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IReadOnlyList<Tarefa>>(consulta.OrderBy(t => t.Id).Select(Copiar).ToList());
        }
    }

    public Task<Tarefa?> ObterAsync(int id, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var tarefa = _tarefas.FirstOrDefault(t => t.Id == id);
            return Task.FromResult(tarefa is null ? null : Copiar(tarefa));
        }
    }

    public Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var nova = Copiar(tarefa);
            nova.Id = ++_ultimoId;
            nova.CriadaEm = DateTimeOffset.UtcNow;
            _tarefas.Add(nova);
            return Task.FromResult(Copiar(nova));
        }
    }

    public Task<bool> AtualizarAsync(Tarefa tarefa, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var indice = _tarefas.FindIndex(t => t.Id == tarefa.Id);
            if (indice < 0) return Task.FromResult(false);
            var atualizada = Copiar(tarefa);
            atualizada.CriadaEm = _tarefas[indice].CriadaEm;
            _tarefas[indice] = atualizada;
            return Task.FromResult(true);
        }
    }

    public Task<bool> ExisteTituloEmAbertoAsync(string titulo, int? ignorarId, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var existe = _tarefas.Any(t =>
                t.Id != ignorarId &&
                t.Situacao != SituacaoTarefa.Concluida &&
                string.Equals(t.Titulo.Trim(), titulo.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(existe);
        }
    }

    public Task<IReadOnlyList<Tarefa>> ListarVencidasAsync(DateOnly hoje, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            return Task.FromResult<IReadOnlyList<Tarefa>>(
                _tarefas.Where(t => t.EstaVencida(hoje)).OrderBy(t => t.Prazo).Select(Copiar).ToList());
        }
    }

    // Devolve cópias para que quem chama não altere a lista interna sem passar pelo serviço.
    private static Tarefa Copiar(Tarefa t) => new()
    {
        Id = t.Id, Titulo = t.Titulo, Descricao = t.Descricao, Situacao = t.Situacao,
        Prioridade = t.Prioridade, Prazo = t.Prazo, CriadaEm = t.CriadaEm
    };
}
