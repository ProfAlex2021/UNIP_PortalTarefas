using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Api.V1;

// DTOs da API v1. São tipos próprios do contrato HTTP: a entidade Tarefa (persistência)
// nunca é serializada diretamente, então mudanças no banco não quebram os clientes.

[Description("Dados para cadastrar uma tarefa.")]
public sealed record TarefaCriacao(
    [property: Required(ErrorMessage = "O título é obrigatório.")]
    [property: StringLength(100, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 100 caracteres.")]
    [property: Description("Título da tarefa (3 a 100 caracteres).")]
    string Titulo,

    [property: StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    [property: Description("Descrição opcional (até 500 caracteres).")]
    string? Descricao = null,

    [property: Description("Prioridade da tarefa. Padrão: Media.")]
    PrioridadeTarefa Prioridade = PrioridadeTarefa.Media,

    [property: Description("Prazo opcional (AAAA-MM-DD). Obrigatório para prioridade Alta; não pode estar no passado.")]
    DateOnly? Prazo = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        var erro = RegrasPrazo.Validar(Prioridade, Prazo, SituacaoTarefa.Pendente, ValidacaoApi.Hoje(contexto));
        if (erro is not null)
            yield return new ValidationResult(erro, [nameof(Prazo)]);
    }
}

[Description("Dados para atualizar uma tarefa. Substitui todos os campos editáveis.")]
public sealed record TarefaAtualizacao(
    [property: Required(ErrorMessage = "O título é obrigatório.")]
    [property: StringLength(100, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 100 caracteres.")]
    string Titulo,

    [property: StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    string? Descricao,

    PrioridadeTarefa Prioridade,

    DateOnly? Prazo,

    SituacaoTarefa Situacao,

    [property: Required(ErrorMessage = "A versão é obrigatória para detectar alterações concorrentes.")]
    [property: Description("Valor de 'versao' recebido no GET. Se a tarefa mudou desde então, a resposta é 409.")]
    string Versao) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        var erro = RegrasPrazo.Validar(Prioridade, Prazo, Situacao, ValidacaoApi.Hoje(contexto));
        if (erro is not null)
            yield return new ValidationResult(erro, [nameof(Prazo)]);
    }
}

[Description("Item da listagem de tarefas.")]
public sealed record TarefaResumo(
    int Id,
    string Titulo,
    SituacaoTarefa Situacao,
    PrioridadeTarefa Prioridade,
    DateOnly? Prazo,
    [property: Description("Verdadeiro quando o prazo já passou e a tarefa não foi concluída.")]
    bool Vencida);

[Description("Representação completa de uma tarefa.")]
public sealed record TarefaDetalhe(
    int Id,
    string Titulo,
    string? Descricao,
    SituacaoTarefa Situacao,
    PrioridadeTarefa Prioridade,
    DateOnly? Prazo,
    DateTimeOffset CriadaEm,
    DateTimeOffset? AtualizadaEm,
    [property: Description("Token de concorrência (Base64). Envie-o de volta no PUT.")]
    string Versao)
{
    public static TarefaDetalhe De(Tarefa t) => new(
        t.Id, t.Titulo, t.Descricao, t.Situacao, t.Prioridade, t.Prazo, t.CriadaEm, t.AtualizadaEm,
        Convert.ToBase64String(t.Versao));
}

[Description("Uma página de tarefas e os metadados de paginação.")]
public sealed record PaginaTarefas(
    IReadOnlyList<TarefaResumo> Itens,
    int Pagina,
    int Tamanho,
    int TotalItens,
    int TotalPaginas);

/// <summary>Parâmetros de consulta da listagem, vinculados da query string com [AsParameters].</summary>
public sealed record ParametrosConsultaTarefas(
    [property: Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    [property: Description("Número da página, começando em 1.")]
    [FromQuery(Name = "pagina")] int Pagina = 1,

    [property: Range(1, ParametrosConsultaTarefas.TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre 1 e 50.")]
    [property: Description("Itens por página (1 a 50).")]
    [FromQuery(Name = "tamanho")] int Tamanho = 10,

    [property: ValoresPermitidos("id", "titulo", "prazo", "prioridade", "situacao", "criadaEm")]
    [property: Description("Campo de ordenação: id, titulo, prazo, prioridade, situacao ou criadaEm.")]
    [FromQuery(Name = "ordenarPor")] string? OrdenarPor = null,

    [property: ValoresPermitidos("asc", "desc")]
    [property: Description("Direção da ordenação: asc ou desc.")]
    [FromQuery(Name = "direcao")] string? Direcao = null,

    [property: Description("Filtra pela situação.")]
    [FromQuery(Name = "situacao")] SituacaoTarefa? Situacao = null,

    [property: Description("Filtra pela prioridade.")]
    [FromQuery(Name = "prioridade")] PrioridadeTarefa? Prioridade = null,

    [property: StringLength(100, ErrorMessage = "A busca deve ter no máximo 100 caracteres.")]
    [property: Description("Texto contido no título.")]
    [FromQuery(Name = "busca")] string? Busca = null)
{
    public const int TamanhoMaximo = 50;
}

/// <summary>Aceita null ou um dos valores informados (sem diferenciar maiúsculas).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValoresPermitidosAttribute(params string[] valores) : ValidationAttribute
{
    public IReadOnlyList<string> Valores { get; } = valores;

    protected override ValidationResult? IsValid(object? value, ValidationContext contexto)
    {
        if (value is null || (value is string texto && Valores.Contains(texto, StringComparer.OrdinalIgnoreCase)))
            return ValidationResult.Success;

        return new ValidationResult($"Valor '{value}' não permitido. Use: {string.Join(", ", Valores)}.",
            contexto.MemberName is null ? null : [contexto.MemberName]);
    }
}

internal static class ValidacaoApi
{
    public static DateOnly Hoje(ValidationContext contexto)
    {
        var relogio = contexto.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(relogio.GetLocalNow().Date);
    }
}
