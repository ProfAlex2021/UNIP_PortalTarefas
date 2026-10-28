using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Modelos;

/// <summary>Representação simples de uma tarefa devolvida pela API.</summary>
public sealed record TarefaDto(int Id, string Titulo, string? Descricao, SituacaoTarefa Situacao)
{
    public static TarefaDto De(Tarefa t) => new(t.Id, t.Titulo, t.Descricao, t.Situacao);
}

/// <summary>Dados recebidos no corpo JSON para cadastrar uma tarefa.</summary>
public sealed record NovaTarefaRequest(
    [property: Required(ErrorMessage = "O título é obrigatório.")]
    [property: StringLength(100, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 100 caracteres.")]
    string Titulo,

    [property: StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    string? Descricao);
