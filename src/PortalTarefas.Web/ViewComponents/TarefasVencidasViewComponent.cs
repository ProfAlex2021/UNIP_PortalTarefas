using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.ViewComponents;

/// <summary>
/// Extensão do Roteiro 2: View Component que mostra as tarefas vencidas.
/// Diferente de uma partial, tem lógica própria e busca os dados por conta própria,
/// por isso pode ser colocado no layout sem que cada controller precise preparar dados.
/// </summary>
public sealed class TarefasVencidasViewComponent(ITarefaService servico, TimeProvider relogio) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int maximo = 5)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        var vencidas = await servico.ListarVencidasAsync(hoje, HttpContext.RequestAborted);
        return View(vencidas.Take(maximo).Select(t => TarefaResumoViewModel.De(t, hoje)).ToList());
    }
}
