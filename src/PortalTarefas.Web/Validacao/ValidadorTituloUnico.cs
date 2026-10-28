using Microsoft.AspNetCore.Mvc.ModelBinding;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Validacao;

/// <summary>
/// Extensão do Roteiro 2: validação assíncrona no servidor para uma regra que depende
/// de dados — não pode haver duas tarefas em aberto com o mesmo título.
/// Fica fora do controller para ser reutilizada e testada isoladamente.
/// </summary>
public sealed class ValidadorTituloUnico(ITarefaService servico)
{
    public async Task ValidarAsync(TarefaInputModel modelo, int? idAtual, ModelStateDictionary modelState,
        CancellationToken cancelamento = default)
    {
        if (string.IsNullOrWhiteSpace(modelo.Titulo))
            return; // a obrigatoriedade já é tratada pelas Data Annotations

        if (await servico.ExisteTituloEmAbertoAsync(modelo.Titulo, idAtual, cancelamento))
            modelState.AddModelError(nameof(TarefaInputModel.Titulo), "Já existe uma tarefa em aberto com este título.");
    }
}
