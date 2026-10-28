using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Validacao;

namespace PortalTarefas.Web.ViewModels;

/// <summary>
/// Dados digitados no formulário de cadastro e edição. Separado do domínio para
/// que a view só receba e envie os campos que o usuário pode alterar.
/// </summary>
[PrazoCompativelComPrioridade]
public sealed class TarefaInputModel
{
    [Required(ErrorMessage = "Informe o título da tarefa.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O título deve ter entre {2} e {1} caracteres.")]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Escolha a prioridade.")]
    [EnumDataType(typeof(PrioridadeTarefa), ErrorMessage = "Prioridade inválida.")]
    [Display(Name = "Prioridade")]
    public PrioridadeTarefa? Prioridade { get; set; } = PrioridadeTarefa.Media;

    [DataType(DataType.Date)]
    [Display(Name = "Prazo")]
    public DateOnly? Prazo { get; set; }

    [EnumDataType(typeof(SituacaoTarefa), ErrorMessage = "Situação inválida.")]
    [Display(Name = "Situação")]
    public SituacaoTarefa Situacao { get; set; } = SituacaoTarefa.Pendente;

    public static TarefaInputModel De(Tarefa t) => new()
    {
        Titulo = t.Titulo,
        Descricao = t.Descricao,
        Prioridade = t.Prioridade,
        Prazo = t.Prazo,
        Situacao = t.Situacao
    };

    public void AplicarEm(Tarefa t)
    {
        t.Titulo = Titulo.Trim();
        t.Descricao = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao.Trim();
        t.Prioridade = Prioridade ?? PrioridadeTarefa.Media;
        t.Prazo = Prazo;
        t.Situacao = Situacao;
    }
}
