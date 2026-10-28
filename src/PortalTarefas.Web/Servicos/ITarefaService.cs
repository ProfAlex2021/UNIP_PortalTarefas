using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>Filtros aceitos na listagem de tarefas.</summary>
public sealed record FiltroTarefas(SituacaoTarefa? Situacao = null, string? Busca = null);

/// <summary>
/// Contrato do serviço de tarefas. Controllers e endpoints dependem desta interface,
/// e não da forma de armazenamento: no Roteiro 2 a implementação é em memória e,
/// no Roteiro 3, passa a ser EF Core sem alterar quem consome o serviço.
/// </summary>
public interface ITarefaService
{
    Task<IReadOnlyList<Tarefa>> ListarAsync(FiltroTarefas filtro, CancellationToken cancelamento = default);
    Task<Tarefa?> ObterAsync(int id, CancellationToken cancelamento = default);
    Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken cancelamento = default);
    Task<bool> AtualizarAsync(Tarefa tarefa, CancellationToken cancelamento = default);
    Task<bool> ExisteTituloEmAbertoAsync(string titulo, int? ignorarId, CancellationToken cancelamento = default);
    Task<IReadOnlyList<Tarefa>> ListarVencidasAsync(DateOnly hoje, CancellationToken cancelamento = default);
}
