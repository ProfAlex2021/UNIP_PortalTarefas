using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Api;

namespace PortalTarefas.Tests.Roteiro06;

/// <summary>Login, logout, sessão e cookie (Roteiro 6).</summary>
public class AutenticacaoTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    private HttpClient ClienteSemCookies() =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private static object Credenciais(string email, string senha) => new { email, senha };

    [Fact]
    public async Task Login_valido_emite_cookie_httponly_secure_e_samesite_strict()
    {
        var resposta = await ClienteSemCookies().PostAsJsonAsync("/api/auth/login",
            Credenciais(FabricaPortal.Emails[Perfil.UsuarioAlfa], fabrica.SenhaDe(Perfil.UsuarioAlfa)));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var cookie = resposta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("PortalTarefas.Sessao="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var sessao = await resposta.Content.ReadFromJsonAsync<Sessao>(Json.Opcoes);
        Assert.True(sessao!.Autenticado);
        Assert.Equal("Alfa", sessao.Equipe);
        Assert.DoesNotContain(fabrica.SenhaDe(Perfil.UsuarioAlfa), await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_invalido_nao_revela_se_a_conta_existe()
    {
        var cliente = ClienteSemCookies();
        var senhaErrada = await cliente.PostAsJsonAsync("/api/auth/login",
            Credenciais(FabricaPortal.Emails[Perfil.UsuarioAlfa], "SenhaErrada123"));
        var contaInexistente = await cliente.PostAsJsonAsync("/api/auth/login",
            Credenciais("ninguem@portaltarefas.local", "SenhaErrada123"));

        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, contaInexistente.StatusCode);
        var titulo1 = JsonNode.Parse(await senhaErrada.Content.ReadAsStringAsync())!["title"]!.GetValue<string>();
        var titulo2 = JsonNode.Parse(await contaInexistente.Content.ReadAsStringAsync())!["title"]!.GetValue<string>();
        Assert.Equal(titulo1, titulo2);
        Assert.False(senhaErrada.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Sessao_anonima_e_sessao_autenticada_com_permissoes_do_servidor()
    {
        var anonima = await ClienteSemCookies().GetFromJsonAsync<Sessao>("/api/auth/sessao", Json.Opcoes);
        var usuario = await (await fabrica.CriarClienteAutenticadoAsync(Perfil.UsuarioAlfa)).GetFromJsonAsync<Sessao>("/api/auth/sessao", Json.Opcoes);
        var admin = await (await fabrica.CriarClienteAutenticadoAsync(Perfil.Administrador)).GetFromJsonAsync<Sessao>("/api/auth/sessao", Json.Opcoes);

        Assert.False(anonima!.Autenticado);
        Assert.Contains("tarefas.criar", usuario!.Permissoes);
        Assert.DoesNotContain("tarefas.excluir", usuario.Permissoes);
        Assert.Contains("tarefas.excluir", admin!.Permissoes);
        Assert.Contains("Administrador", admin.Papeis);
    }

    [Fact]
    public async Task Logout_invalida_o_cookie_mesmo_que_ele_seja_reenviado()
    {
        var cliente = ClienteSemCookies();
        var login = await cliente.PostAsJsonAsync("/api/auth/login",
            Credenciais(FabricaPortal.Emails[Perfil.UsuarioBeta], fabrica.SenhaDe(Perfil.UsuarioBeta)));
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("PortalTarefas.Sessao=")).Split(';')[0];

        HttpRequestMessage Requisicao(HttpMethod metodo, string url)
        {
            var r = new HttpRequestMessage(metodo, url);
            r.Headers.Add("Cookie", cookie);
            r.Headers.Add("X-Requested-With", "PortalTarefas");
            return r;
        }

        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(Requisicao(HttpMethod.Get, "/api/v1/tarefas"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(Requisicao(HttpMethod.Post, "/api/auth/logout"))).StatusCode);

        // O mesmo cookie, reenviado depois do logout, é recusado.
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.SendAsync(Requisicao(HttpMethod.Get, "/api/v1/tarefas"))).StatusCode);
    }

    [Fact]
    public async Task Requisicao_que_altera_dados_sem_cabecalho_anti_csrf_e_recusada()
    {
        var cliente = await fabrica.CriarClienteAutenticadoAsync(Perfil.UsuarioAlfa);
        cliente.DefaultRequestHeaders.Remove("X-Requested-With");

        var resposta = await cliente.PostAsJsonAsync("/api/v1/tarefas", new { titulo = "Tentativa CSRF" });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Contains("CSRF", await resposta.Content.ReadAsStringAsync());
    }
}

/// <summary>Bloqueio de conta após tentativas falhas (fábrica própria para não afetar outros testes).</summary>
public class BloqueioDeContaTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    [Fact]
    public async Task Apos_cinco_falhas_a_conta_e_bloqueada_e_a_resposta_continua_generica()
    {
        var cliente = fabrica.CriarClienteHttps();
        var email = FabricaPortal.Emails[Perfil.UsuarioBeta];

        for (var i = 0; i < 5; i++)
            await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = "SenhaErrada123" });

        // Mesmo com a senha correta, a conta está bloqueada — e a resposta não diz isso.
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = fabrica.SenhaDe(Perfil.UsuarioBeta) });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("E-mail ou senha inválidos.", await resposta.Content.ReadAsStringAsync());
    }
}
