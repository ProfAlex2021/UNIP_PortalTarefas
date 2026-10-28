using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>
/// Implementação em memória criada no Roteiro 2. Desde o Roteiro 3 a aplicação usa
/// <see cref="TarefaServiceEf"/>; esta classe permanece como dublê nos testes unitários
/// do controller, que assim não precisam de banco de dados.
/// </summary>
public sealed class TarefaServiceEmMemoria : ITarefaService
{
    private readonly Lock _trava = new();
    private readonly List<Tarefa> _tarefas;
    private readonly List<EventoAuditoria> _eventos = [];
    private readonly TimeProvider _relogio;
    private int _ultimoId;
    private long _ultimaVersao;

    public TarefaServiceEmMemoria(TimeProvider relogio)
    {
        _relogio = relogio;
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        _tarefas =
        [
            new() { Id = 1, Titulo = "Configurar ambiente .NET 10", Descricao = "Instalar SDK e Visual Studio 2026",
                    Situacao = SituacaoTarefa.Concluida, Prioridade = PrioridadeTarefa.Alta, Prazo = hoje.AddDays(-10) },
            new() { Id = 2, Titulo = "Estudar pipeline HTTP", Descricao = "Ler a Unidade I",
                    Situacao = SituacaoTarefa.EmAndamento, Prioridade = PrioridadeTarefa.Media, Prazo = hoje.AddDays(5) },
            new() { Id = 3, Titulo = "Entregar relatório do Roteiro 1",
                    Situacao = SituacaoTarefa.Pendente, Prioridade = PrioridadeTarefa.Alta, Prazo = hoje.AddDays(-2) },
        ];
        foreach (var t in _tarefas)
        {
            t.CriadaEm = relogio.GetUtcNow();
            t.Versao = NovaVersao();
        }
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
            nova.CriadaEm = _relogio.GetUtcNow();
            nova.Versao = NovaVersao();
            _tarefas.Add(nova);
            return Task.FromResult(Copiar(nova));
        }
    }

    public Task<ResultadoAtualizacao> AtualizarAsync(Tarefa alterada, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var indice = _tarefas.FindIndex(t => t.Id == alterada.Id);
            if (indice < 0)
                return Task.FromResult(new ResultadoAtualizacao(ResultadoOperacao.NaoEncontrada));

            var atual = _tarefas[indice];
            if (!atual.Versao.AsSpan().SequenceEqual(alterada.Versao))
                return Task.FromResult(new ResultadoAtualizacao(ResultadoOperacao.Conflito, Copiar(atual)));

            var nova = Copiar(alterada);
            nova.CriadaEm = atual.CriadaEm;
            nova.Versao = NovaVersao();
            _tarefas[indice] = nova;
            return Task.FromResult(new ResultadoAtualizacao(ResultadoOperacao.Sucesso, Copiar(nova)));
        }
    }

    public Task<ResultadoOperacao> AlterarSituacaoAsync(int id, SituacaoTarefa novaSituacao, string? observacao,
        CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            var tarefa = _tarefas.FirstOrDefault(t => t.Id == id);
            if (tarefa is null) return Task.FromResult(ResultadoOperacao.NaoEncontrada);
            _eventos.Add(new EventoAuditoria
            {
                TarefaId = id, Acao = "SituacaoAlterada", Detalhe = $"{tarefa.Situacao} -> {novaSituacao}",
                OcorridoEm = _relogio.GetUtcNow()
            });
            tarefa.Situacao = novaSituacao;
            tarefa.Versao = NovaVersao();
            return Task.FromResult(ResultadoOperacao.Sucesso);
        }
    }

    public Task<bool> ExcluirAsync(int id, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            _eventos.RemoveAll(e => e.TarefaId == id);
            return Task.FromResult(_tarefas.RemoveAll(t => t.Id == id) > 0);
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

    public Task<IReadOnlyList<EventoAuditoria>> ListarEventosAsync(int tarefaId, CancellationToken cancelamento = default)
    {
        lock (_trava)
        {
            return Task.FromResult<IReadOnlyList<EventoAuditoria>>(_eventos.Where(e => e.TarefaId == tarefaId).ToList());
        }
    }

    private byte[] NovaVersao() => BitConverter.GetBytes(++_ultimaVersao);

    // Devolve cópias para que quem chama não altere a lista interna sem passar pelo serviço.
    private static Tarefa Copiar(Tarefa t) => new()
    {
        Id = t.Id, Titulo = t.Titulo, Descricao = t.Descricao, Situacao = t.Situacao,
        Prioridade = t.Prioridade, Prazo = t.Prazo, CriadaEm = t.CriadaEm, AtualizadaEm = t.AtualizadaEm,
        Versao = t.Versao.ToArray(), Equipe = t.Equipe, CriadaPorId = t.CriadaPorId
    };
}
