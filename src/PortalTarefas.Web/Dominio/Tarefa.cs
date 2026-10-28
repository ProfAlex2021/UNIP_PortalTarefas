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

/// <summary>
/// Entidade persistida de uma tarefa. Não é exposta diretamente em views nem na API:
/// a conversão para ViewModels e DTOs acontece nos controllers e endpoints.
/// O mapeamento para o banco está em Dados/Configuracoes/TarefaConfiguracao.cs.
/// </summary>
public class Tarefa
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public SituacaoTarefa Situacao { get; set; } = SituacaoTarefa.Pendente;
    public PrioridadeTarefa Prioridade { get; set; } = PrioridadeTarefa.Media;
    public DateOnly? Prazo { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset? AtualizadaEm { get; set; }

    /// <summary>Token de concorrência (rowversion no SQL Server), alterado pelo banco a cada UPDATE.</summary>
    public byte[] Versao { get; set; } = [];

    public List<EventoAuditoria> Eventos { get; set; } = [];

    /// <summary>Uma tarefa está vencida quando tem prazo anterior a hoje e não foi concluída.</summary>
    public bool EstaVencida(DateOnly hoje) =>
        Prazo is not null && Prazo < hoje && Situacao != SituacaoTarefa.Concluida;
}

/// <summary>Registro de auditoria gravado na mesma transação da alteração da tarefa.</summary>
public class EventoAuditoria
{
    public long Id { get; set; }
    public int TarefaId { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? Detalhe { get; set; }
    public DateTimeOffset OcorridoEm { get; set; }
}
