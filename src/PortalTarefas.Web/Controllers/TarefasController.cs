using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.Validacao;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Controllers;

/// <summary>
/// Interface MVC de tarefas. O controller apenas coordena: recebe o modelo,
/// verifica ModelState, chama o serviço e escolhe a view. Regras de negócio e
/// acesso a dados ficam no serviço e nos validadores.
/// </summary>
[ServiceFilter<RegistrarExecucaoAcaoFilter>] // escopo: só as ações deste controller
public sealed class TarefasController(
    ITarefaService servico,
    ValidadorTituloUnico validadorTitulo,
    TimeProvider relogio) : Controller
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().Date);

    // GET /Tarefas?busca=...&situacao=...
    public async Task<IActionResult> Index(string? busca, SituacaoTarefa? situacao, CancellationToken cancelamento)
    {
        var tarefas = await servico.ListarAsync(new FiltroTarefas(situacao, busca), cancelamento);
        return View(new TarefasIndexViewModel
        {
            Busca = busca,
            Situacao = situacao,
            Tarefas = tarefas.Select(t => TarefaResumoViewModel.De(t, Hoje)).ToList()
        });
    }

    // GET /Tarefas/Details/5
    public async Task<IActionResult> Details(int id, CancellationToken cancelamento)
    {
        var tarefa = await servico.ObterAsync(id, cancelamento);
        if (tarefa is null)
            return NaoEncontrada(id);

        return View(new TarefaDetalheViewModel(TarefaResumoViewModel.De(tarefa, Hoje), tarefa.Descricao, tarefa.CriadaEm));
    }

    // GET /Tarefas/Create
    public IActionResult Create() => View(new TarefaInputModel());

    // POST /Tarefas/Create
    [HttpPost]
    public async Task<IActionResult> Create(TarefaInputModel modelo, CancellationToken cancelamento)
    {
        await validadorTitulo.ValidarAsync(modelo, idAtual: null, ModelState, cancelamento);
        if (!ModelState.IsValid)
            return View(modelo); // volta ao formulário preservando o que foi digitado

        var tarefa = new Tarefa();
        modelo.AplicarEm(tarefa);
        var criada = await servico.CriarAsync(tarefa, cancelamento);

        TempData["Mensagem"] = $"Tarefa \"{criada.Titulo}\" cadastrada.";
        return RedirectToAction(nameof(Details), new { id = criada.Id });
    }

    // GET /Tarefas/Edit/5
    public async Task<IActionResult> Edit(int id, CancellationToken cancelamento)
    {
        var tarefa = await servico.ObterAsync(id, cancelamento);
        return tarefa is null ? NaoEncontrada(id) : View(TarefaInputModel.De(tarefa));
    }

    // POST /Tarefas/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, TarefaInputModel modelo, CancellationToken cancelamento)
    {
        var tarefa = await servico.ObterAsync(id, cancelamento);
        if (tarefa is null)
            return NaoEncontrada(id);

        await validadorTitulo.ValidarAsync(modelo, idAtual: id, ModelState, cancelamento);
        if (!ModelState.IsValid)
            return View(modelo);

        modelo.AplicarEm(tarefa);
        await servico.AtualizarAsync(tarefa, cancelamento);

        TempData["Mensagem"] = "Alterações salvas.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private ViewResult NaoEncontrada(int id)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NaoEncontrada", id);
    }
}
