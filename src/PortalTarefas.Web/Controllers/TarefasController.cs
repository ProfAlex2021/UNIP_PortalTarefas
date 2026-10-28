using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Seguranca;
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
[Authorize(Policy = Politicas.LerTarefas)]     // Roteiro 6: leitura privada
public sealed class TarefasController(
    ITarefaService servico,
    ValidadorTituloUnico validadorTitulo,
    TimeProvider relogio,
    IAuthorizationService autorizacao) : Controller
{
    public const string MensagemConflito =
        "Esta tarefa foi alterada por outra pessoa depois que você abriu o formulário. " +
        "Nada foi sobrescrito. Confira os valores atuais abaixo e salve novamente se quiser manter os seus.";

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

        var eventos = await servico.ListarEventosAsync(id, cancelamento);
        return View(new TarefaDetalheViewModel(
            TarefaResumoViewModel.De(tarefa, Hoje),
            tarefa.Descricao,
            tarefa.CriadaEm,
            eventos.Select(EventoAuditoriaViewModel.De).ToList()));
    }

    // GET /Tarefas/Create
    [Authorize(Policy = Politicas.CriarTarefas)]
    public IActionResult Create() => View(new TarefaInputModel());

    // POST /Tarefas/Create
    [HttpPost]
    [Authorize(Policy = Politicas.CriarTarefas)]
    public async Task<IActionResult> Create(TarefaInputModel modelo, CancellationToken cancelamento)
    {
        await validadorTitulo.ValidarAsync(modelo, idAtual: null, ModelState, cancelamento);
        if (!ModelState.IsValid)
            return View(modelo); // volta ao formulário preservando o que foi digitado

        var tarefa = new Tarefa { Equipe = User.ObterEquipe() ?? Tarefa.EquipePadrao, CriadaPorId = User.ObterId() };
        modelo.AplicarEm(tarefa);
        var criada = await servico.CriarAsync(tarefa, cancelamento);

        TempData["Mensagem"] = $"Tarefa \"{criada.Titulo}\" cadastrada.";
        return RedirectToAction(nameof(Details), new { id = criada.Id });
    }

    // GET /Tarefas/Edit/5
    public async Task<IActionResult> Edit(int id, CancellationToken cancelamento)
    {
        var tarefa = await servico.ObterAsync(id, cancelamento);
        if (tarefa is null) return NaoEncontrada(id);
        if (!await PodeEditarAsync(tarefa)) return Forbid();
        return View(TarefaInputModel.De(tarefa));
    }

    // POST /Tarefas/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, TarefaInputModel modelo, CancellationToken cancelamento)
    {
        var existente = await servico.ObterAsync(id, cancelamento);
        if (existente is null) return NaoEncontrada(id);
        if (!await PodeEditarAsync(existente)) return Forbid();

        if (!TentarLerVersao(modelo.Versao, out var versao))
            ModelState.AddModelError(string.Empty, "Versão da tarefa ausente ou inválida. Abra a edição novamente.");

        await validadorTitulo.ValidarAsync(modelo, idAtual: id, ModelState, cancelamento);
        if (!ModelState.IsValid)
            return View(modelo);

        var alterada = new Tarefa { Id = id, Versao = versao, Equipe = existente.Equipe, CriadaPorId = existente.CriadaPorId };
        modelo.AplicarEm(alterada);
        var resultado = await servico.AtualizarAsync(alterada, cancelamento);

        switch (resultado.Resultado)
        {
            case ResultadoOperacao.NaoEncontrada:
                return NaoEncontrada(id);

            case ResultadoOperacao.Conflito:
                ModelState.AddModelError(string.Empty, MensagemConflito);
                ViewData["ValoresAtuais"] = TarefaInputModel.De(resultado.Atual!);
                // A nova versão permite salvar de novo, agora de forma consciente.
                ModelState.Remove(nameof(TarefaInputModel.Versao));
                modelo.Versao = Convert.ToBase64String(resultado.Atual!.Versao);
                return View(modelo);

            default:
                TempData["Mensagem"] = "Alterações salvas.";
                return RedirectToAction(nameof(Details), new { id });
        }
    }

    // POST /Tarefas/AlterarSituacao/5 — usa a transação explícita do serviço.
    [HttpPost]
    public async Task<IActionResult> AlterarSituacao(int id, SituacaoTarefa novaSituacao, string? observacao,
        CancellationToken cancelamento)
    {
        if (observacao?.Length > 150)
        {
            TempData["Mensagem"] = "A observação deve ter no máximo 150 caracteres.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var existente = await servico.ObterAsync(id, cancelamento);
        if (existente is null) return NaoEncontrada(id);
        if (!await PodeEditarAsync(existente)) return Forbid();

        await servico.AlterarSituacaoAsync(id, novaSituacao, observacao, cancelamento);

        TempData["Mensagem"] = $"Situação alterada para {novaSituacao.Exibir()}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Autorização por recurso: mesma política usada pela API.</summary>
    private async Task<bool> PodeEditarAsync(Tarefa tarefa) =>
        (await autorizacao.AuthorizeAsync(User, tarefa, Politicas.EditarTarefa)).Succeeded;

    private ViewResult NaoEncontrada(int id)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NaoEncontrada", id);
    }

    private static bool TentarLerVersao(string? base64, out byte[] versao)
    {
        versao = [];
        if (string.IsNullOrWhiteSpace(base64)) return false;
        versao = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, versao, out var tamanho)) return false;
        versao = versao[..tamanho];
        return true;
    }
}
