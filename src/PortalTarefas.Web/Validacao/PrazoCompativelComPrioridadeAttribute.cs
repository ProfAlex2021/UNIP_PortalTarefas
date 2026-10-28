using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Validacao;

/// <summary>
/// Validação customizada que envolve mais de um campo:
/// 1) tarefa de prioridade Alta precisa de prazo;
/// 2) o prazo não pode estar no passado, a menos que a tarefa já esteja concluída.
/// O erro é associado ao campo Prazo para aparecer ao lado dele no formulário.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PrazoCompativelComPrioridadeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext contexto)
    {
        if (value is not TarefaInputModel modelo)
            return ValidationResult.Success;

        if (modelo.Prioridade == PrioridadeTarefa.Alta && modelo.Prazo is null)
            return new ValidationResult("Tarefas de prioridade Alta precisam de um prazo.", [nameof(TarefaInputModel.Prazo)]);

        var relogio = contexto.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);

        if (modelo.Prazo < hoje && modelo.Situacao != SituacaoTarefa.Concluida)
            return new ValidationResult("O prazo não pode ser anterior a hoje para tarefas não concluídas.", [nameof(TarefaInputModel.Prazo)]);

        return ValidationResult.Success;
    }
}
