using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>Filtros aceitos na listagem de tarefas.</summary>
public sealed record FiltroTarefas(SituacaoTarefa? Situacao = null, string? Busca = null);

public enum ResultadoOperacao
{
    Sucesso,
    NaoEncontrada,
    Conflito
}

/// <summary>
/// Resultado de uma atualização. Em caso de conflito de concorrência, <see cref="Atual"/>
/// traz os valores que estão no banco para que o usuário decida o que fazer.
/// </summary>
public sealed record ResultadoAtualizacao(ResultadoOperacao Resultado, Tarefa? Atual = null);

/// <summary>
/// Contrato do serviço de tarefas. Controllers e endpoints dependem desta interface,
/// e não da forma de armazenamento: no Roteiro 2 a implementação era em memória e,
/// no Roteiro 3, passa a ser EF Core sem alterar quem consome o serviço.
/// </summary>
public interface ITarefaService
{
    Task<IReadOnlyList<Tarefa>> ListarAsync(FiltroTarefas filtro, CancellationToken cancelamento = default);
    Task<Tarefa?> ObterAsync(int id, CancellationToken cancelamento = default);
    Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken cancelamento = default);

    /// <summary>
    /// Atualiza os campos editáveis usando <see cref="Tarefa.Versao"/> como token de concorrência.
    /// A alteração e o evento de auditoria são gravados no mesmo SaveChanges (transação implícita).
    /// </summary>
    Task<ResultadoAtualizacao> AtualizarAsync(Tarefa alterada, CancellationToken cancelamento = default);

    /// <summary>
    /// Altera a situação e registra auditoria em uma transação EXPLÍCITA com duas gravações.
    /// Se a segunda gravação falhar, a primeira é desfeita (rollback).
    /// </summary>
    Task<ResultadoOperacao> AlterarSituacaoAsync(int id, SituacaoTarefa novaSituacao, string? observacao,
        CancellationToken cancelamento = default);

    Task<bool> ExisteTituloEmAbertoAsync(string titulo, int? ignorarId, CancellationToken cancelamento = default);
    Task<IReadOnlyList<Tarefa>> ListarVencidasAsync(DateOnly hoje, CancellationToken cancelamento = default);
    Task<IReadOnlyList<EventoAuditoria>> ListarEventosAsync(int tarefaId, CancellationToken cancelamento = default);
}
