using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Validacao;

/// <summary>
/// Validação customizada que envolve mais de um campo:
/// 1) tarefa de prioridade Alta precisa de prazo;
/// 2) o prazo não pode estar no passado, a menos que a tarefa já esteja concluída.
/// O erro é associado ao campo Prazo para aparecer ao lado dele no formulário.
/// A regra em si está em <see cref="RegrasPrazo"/>, compartilhada com a API.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PrazoCompativelComPrioridadeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext contexto)
    {
        if (value is not TarefaInputModel modelo)
            return ValidationResult.Success;

        var relogio = contexto.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);

        var erro = RegrasPrazo.Validar(modelo.Prioridade, modelo.Prazo, modelo.Situacao, hoje);
        return erro is null
            ? ValidationResult.Success
            : new ValidationResult(erro, [nameof(TarefaInputModel.Prazo)]);
    }
}
