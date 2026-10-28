using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PortalTarefas.Tests.Roteiro02;

/// <summary>
/// Testes de integração da interface MVC: renderização com layout compartilhado,
/// acesso a id inexistente e envio de formulário inválido (com token antiforgery).
/// </summary>
public partial class InterfaceMvcTests(WebApplicationFactory<Program> fabrica)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _cliente = fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("/")]
    [InlineData("/Tarefas")]
    [InlineData("/Tarefas/Details/1")]
    [InlineData("/Tarefas/Create")]
    public async Task Paginas_compartilham_o_mesmo_layout(string url)
    {
        var html = await _cliente.GetStringAsync(url);

        Assert.Contains("class=\"marca\"", html);              // cabeçalho do _Layout
        Assert.Contains("<h2>Tarefas vencidas</h2>", html);   // View Component no layout
    }

    [Fact]
    public async Task Detalhes_de_id_inexistente_retorna_404_com_pagina_amigavel()
    {
        var resposta = await _cliente.GetAsync("/Tarefas/Details/999");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Contains("Tarefa não encontrada", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Formulario_com_titulo_vazio_volta_com_erro_e_preserva_dados()
    {
        var token = await ObterTokenAntiforgery("/Tarefas/Create");

        var resposta = await _cliente.PostAsync("/Tarefas/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Titulo"] = "",
            ["Descricao"] = "Texto que deve ser preservado",
            ["Prioridade"] = "1",
            ["Situacao"] = "0"
        }));
        var html = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe o título da tarefa.", html);
        Assert.Contains("Texto que deve ser preservado", html);
    }

    [Fact]
    public async Task Post_sem_token_antiforgery_e_rejeitado()
    {
        var resposta = await _cliente.PostAsync("/Tarefas/Create",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Titulo"] = "Sem token" }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private async Task<string> ObterTokenAntiforgery(string url)
    {
        var html = await _cliente.GetStringAsync(url);
        return TokenRegex().Match(html).Groups[1].Value;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
