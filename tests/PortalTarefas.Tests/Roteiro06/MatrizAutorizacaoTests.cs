using System.Net;
using System.Net.Http.Json;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Api.V1;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Tests.Roteiro06;

/// <summary>
/// Matriz 401/403/200 com usuário anônimo, usuário comum e administrador.
/// As requisições são feitas direto na API, sem a SPA: o servidor é a autoridade final.
/// </summary>
public class MatrizAutorizacaoTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    private const string Base = "/api/v1/tarefas";

    private async Task<HttpClient> Cliente(string perfil) => perfil switch
    {
        "anonimo" => fabrica.CriarClienteHttps(),
        "usuario" => await fabrica.CriarClienteAutenticadoAsync(Perfil.UsuarioAlfa),
        "admin" => await fabrica.CriarClienteAutenticadoAsync(Perfil.Administrador),
        _ => throw new ArgumentException(perfil)
    };

    private async Task<TarefaDetalhe> CriarComo(Perfil perfil, string titulo)
    {
        var cliente = await fabrica.CriarClienteAutenticadoAsync(perfil);
        var resposta = await cliente.PostAsJsonAsync(Base, new TarefaCriacao(titulo), Json.Opcoes);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TarefaDetalhe>(Json.Opcoes))!;
    }

    private static TarefaAtualizacao Edicao(TarefaDetalhe t, string titulo) =>
        new(titulo, t.Descricao, t.Prioridade, t.Prazo, t.Situacao, t.Versao);

    [Theory]
    [InlineData("anonimo", HttpStatusCode.Unauthorized)]
    [InlineData("usuario", HttpStatusCode.OK)]
    [InlineData("admin", HttpStatusCode.OK)]
    public async Task Leitura_privada(string perfil, HttpStatusCode esperado)
    {
        var resposta = await (await Cliente(perfil)).GetAsync(Base);
        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Theory]
    [InlineData("anonimo", HttpStatusCode.Unauthorized)]
    [InlineData("usuario", HttpStatusCode.Created)]
    [InlineData("admin", HttpStatusCode.Created)]
    public async Task Criacao(string perfil, HttpStatusCode esperado)
    {
        var resposta = await (await Cliente(perfil)).PostAsJsonAsync(Base, new TarefaCriacao($"Criação por {perfil} {Guid.NewGuid():N}"), Json.Opcoes);
        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Theory]
    [InlineData("anonimo", HttpStatusCode.Unauthorized)]
    [InlineData("usuario", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.NoContent)]
    public async Task Exclusao_somente_administrador(string perfil, HttpStatusCode esperado)
    {
        var tarefa = await CriarComo(Perfil.Administrador, $"Para excluir {Guid.NewGuid():N}");
        var resposta = await (await Cliente(perfil)).DeleteAsync($"{Base}/{tarefa.Id}");
        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Fact]
    public async Task Tarefa_criada_herda_a_equipe_de_quem_criou()
    {
        var tarefa = await CriarComo(Perfil.UsuarioBeta, $"Da equipe Beta {Guid.NewGuid():N}");
        Assert.Equal("Beta", tarefa.Equipe);
    }

    [Fact]
    public async Task Usuario_de_outra_equipe_nao_edita_403()
    {
        var daAlfa = await CriarComo(Perfil.UsuarioAlfa, $"Tarefa da Alfa {Guid.NewGuid():N}");
        var bruno = await fabrica.CriarClienteAutenticadoAsync(Perfil.UsuarioBeta);

        var resposta = await bruno.PutAsJsonAsync($"{Base}/{daAlfa.Id}", Edicao(daAlfa, "Bruno tentou editar"), Json.Opcoes);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Usuario_da_mesma_equipe_edita_200()
    {
        var daAlfa = await CriarComo(Perfil.Administrador, $"Criada pela admin na Alfa {Guid.NewGuid():N}");
        var ana = await fabrica.CriarClienteAutenticadoAsync(Perfil.UsuarioAlfa);

        var resposta = await ana.PutAsJsonAsync($"{Base}/{daAlfa.Id}", Edicao(daAlfa, $"Ana editou {Guid.NewGuid():N}"), Json.Opcoes);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Claim_editar_outras_equipes_permite_editar_tarefa_da_beta()
    {
        var daBeta = await CriarComo(Perfil.UsuarioBeta, $"Tarefa da Beta {Guid.NewGuid():N}");
        var admin = await fabrica.CriarClienteAutenticadoAsync(Perfil.Administrador);

        var resposta = await admin.PutAsJsonAsync($"{Base}/{daBeta.Id}", Edicao(daBeta, $"Admin editou {Guid.NewGuid():N}"), Json.Opcoes);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Endpoints_legados_do_roteiro_1_tambem_exigem_login()
    {
        var anonimo = fabrica.CriarClienteHttps();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/tarefas")).StatusCode);
    }

    [Fact]
    public async Task Pagina_mvc_protegida_redireciona_anonimo_para_login()
    {
        var anonimo = fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var resposta = await anonimo.GetAsync("/Tarefas");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.StartsWith("/Conta/Login", resposta.Headers.Location!.PathAndQuery);
    }
}
