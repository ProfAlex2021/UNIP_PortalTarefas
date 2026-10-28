using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Dominio;

public enum SituacaoTarefa
{
    Pendente,
    [Display(Name = "Em andamento")] EmAndamento,
    [Display(Name = "Concluída")] Concluida
}

public enum PrioridadeTarefa
{
    Baixa,
    [Display(Name = "Média")] Media,
    Alta
}

/// <summary>Modelo de domínio de uma tarefa. Não é exposto diretamente em views nem na API.</summary>
public class Tarefa
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public SituacaoTarefa Situacao { get; set; } = SituacaoTarefa.Pendente;
    public PrioridadeTarefa Prioridade { get; set; } = PrioridadeTarefa.Media;
    public DateOnly? Prazo { get; set; }
    public DateTimeOffset CriadaEm { get; set; }

    /// <summary>Uma tarefa está vencida quando tem prazo anterior a hoje e não foi concluída.</summary>
    public bool EstaVencida(DateOnly hoje) =>
        Prazo is not null && Prazo < hoje && Situacao != SituacaoTarefa.Concluida;
}
