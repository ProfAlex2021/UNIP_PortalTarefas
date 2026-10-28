using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.ViewModels;

/// <summary>Dados mínimos para exibir uma tarefa em lista ou cartão (partial _TarefaResumo).</summary>
public sealed record TarefaResumoViewModel(
    int Id, string Titulo, SituacaoTarefa Situacao, PrioridadeTarefa Prioridade, DateOnly? Prazo, bool Vencida)
{
    public static TarefaResumoViewModel De(Tarefa t, DateOnly hoje) =>
        new(t.Id, t.Titulo, t.Situacao, t.Prioridade, t.Prazo, t.EstaVencida(hoje));
}

/// <summary>Modelo da página de listagem, com filtros e resultados.</summary>
public sealed class TarefasIndexViewModel
{
    public string? Busca { get; init; }
    public SituacaoTarefa? Situacao { get; init; }
    public IReadOnlyList<TarefaResumoViewModel> Tarefas { get; init; } = [];
}

/// <summary>Modelo da página de detalhes.</summary>
public sealed record TarefaDetalheViewModel(TarefaResumoViewModel Resumo, string? Descricao, DateTimeOffset CriadaEm);
