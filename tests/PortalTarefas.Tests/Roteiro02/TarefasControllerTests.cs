using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Controllers;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Seguranca;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.Validacao;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Tests.Roteiro02;

/// <summary>
/// Testes unitários do controller: instanciamos o controller diretamente, com o
/// serviço em memória, sem subir servidor HTTP.
/// </summary>
public class TarefasControllerTests
{
    private readonly TarefaServiceEmMemoria _servico = new(RelogioFixo.Padrao);

    private TarefasController CriarController()
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, Papeis.Usuario), new Claim(TiposClaim.Equipe, "Alfa")], "Teste"))
        };
        return new TarefasController(_servico, new ValidadorTituloUnico(_servico), RelogioFixo.Padrao, new AutorizacaoPermitida())
        {
            ControllerContext = new ControllerContext { HttpContext = contexto },
            TempData = new TempDataDictionary(contexto, new TempDataEmMemoria())
        };
    }

    [Fact]
    public async Task Create_com_modelstate_invalido_volta_para_a_view_com_os_dados()
    {
        var controller = CriarController();
        var modelo = new TarefaInputModel { Titulo = "", Descricao = "texto digitado" };
        controller.ModelState.AddModelError(nameof(TarefaInputModel.Titulo), "Informe o título da tarefa.");

        var resultado = await controller.Create(modelo, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.Same(modelo, view.Model);
    }

    [Fact]
    public async Task Create_valido_salva_e_redireciona_para_detalhes()
    {
        var controller = CriarController();
        var modelo = new TarefaInputModel { Titulo = "Tarefa criada no teste", Prioridade = PrioridadeTarefa.Baixa };

        var resultado = await controller.Create(modelo, CancellationToken.None);

        var redirecionamento = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(TarefasController.Details), redirecionamento.ActionName);
        var tarefas = await _servico.ListarAsync(new FiltroTarefas(Busca: "criada no teste"));
        Assert.Single(tarefas);
    }

    [Fact]
    public async Task Create_com_titulo_duplicado_em_aberto_e_rejeitado()
    {
        var controller = CriarController();
        var modelo = new TarefaInputModel { Titulo = "Estudar pipeline HTTP" }; // já existe e está em andamento

        var resultado = await controller.Create(modelo, CancellationToken.None);

        Assert.IsType<ViewResult>(resultado);
        Assert.True(controller.ModelState.ContainsKey(nameof(TarefaInputModel.Titulo)));
    }

    [Fact]
    public async Task Details_de_id_inexistente_retorna_404()
    {
        var controller = CriarController();

        var resultado = await controller.Details(999, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.Equal("NaoEncontrada", view.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Details_de_id_existente_retorna_modelo_de_detalhes()
    {
        var resultado = await CriarController().Details(1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(resultado);
        var modelo = Assert.IsType<TarefaDetalheViewModel>(view.Model);
        Assert.Equal(1, modelo.Resumo.Id);
    }

    /// <summary>Dublê: nos testes unitários a autorização sempre permite (ela é testada no Roteiro 6).</summary>
    private sealed class AutorizacaoPermitida : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(AuthorizationResult.Success());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class TempDataEmMemoria : ITempDataProvider
    {
        private IDictionary<string, object> _dados = new Dictionary<string, object>();
        public IDictionary<string, object> LoadTempData(HttpContext context) => _dados;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _dados = values;
    }
}
