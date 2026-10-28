using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Api.V1;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Tests.Roteiro04;

/// <summary>Testes de integração da API REST v1 contra o contrato (docs/contrato-api-v1.md).</summary>
public class ApiV1Tests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>, IAsyncLifetime
{
    private const string Base = "/api/v1/tarefas";
    // Desde o Roteiro 6 a API e as páginas exigem login: os testes usam um cliente autenticado.
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await fabrica.CriarClienteAutenticadoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<TarefaDetalhe> CriarAsync(string titulo, PrioridadeTarefa prioridade = PrioridadeTarefa.Baixa)
    {
        var resposta = await _cliente.PostAsJsonAsync(Base, new TarefaCriacao(titulo, Prioridade: prioridade), Json.Opcoes);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TarefaDetalhe>(Json.Opcoes))!;
    }

    private static async Task<JsonObject> LerProblema(HttpResponseMessage resposta)
    {
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject();
    }

    // ---------- Paginação ----------

    [Theory]
    [InlineData("tamanho=0", "tamanho")]
    [InlineData("tamanho=51", "tamanho")]
    [InlineData("pagina=0", "pagina")]
    public async Task Paginacao_fora_dos_limites_retorna_400(string consulta, string campo)
    {
        var resposta = await _cliente.GetAsync($"{Base}?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await LerProblema(resposta);
        Assert.NotNull(problema["errors"]![campo]);
    }

    [Fact]
    public async Task Paginacao_nos_limites_minimo_e_maximo_e_aceita()
    {
        var minimo = await _cliente.GetFromJsonAsync<PaginaTarefas>($"{Base}?pagina=1&tamanho=1", Json.Opcoes);
        var maximo = await _cliente.GetFromJsonAsync<PaginaTarefas>($"{Base}?tamanho=50", Json.Opcoes);

        Assert.Single(minimo!.Itens);
        Assert.Equal(50, maximo!.Tamanho);
        Assert.Equal(maximo.TotalItens, maximo.Itens.Count);
    }

    [Fact]
    public async Task Pagina_alem_do_fim_retorna_lista_vazia_com_metadados()
    {
        var resposta = await _cliente.GetAsync($"{Base}?pagina=999");
        var pagina = await resposta.Content.ReadFromJsonAsync<PaginaTarefas>(Json.Opcoes);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Empty(pagina!.Itens);
        Assert.True(pagina.TotalItens > 0);
        Assert.Equal(pagina.TotalItens.ToString(), resposta.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task Cabecalho_link_preserva_filtros_e_aponta_proxima_pagina()
    {
        var resposta = await _cliente.GetAsync($"{Base}?tamanho=2&situacao=Pendente&pagina=1");
        var link = resposta.Headers.GetValues("Link").Single();

        Assert.Contains("rel=\"next\"", link);
        Assert.Contains("situacao=Pendente", link);
        Assert.Contains("pagina=2", link);
    }

    // ---------- Ordenação e filtros ----------

    [Fact]
    public async Task Ordenacao_por_campo_permitido_funciona()
    {
        var pagina = await _cliente.GetFromJsonAsync<PaginaTarefas>($"{Base}?ordenarPor=titulo&direcao=desc&tamanho=50", Json.Opcoes);

        var titulos = pagina!.Itens.Select(t => t.Titulo).ToList();
        Assert.Equal(titulos.OrderByDescending(t => t, StringComparer.OrdinalIgnoreCase), titulos, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ordenacao_por_campo_nao_permitido_e_rejeitada()
    {
        var resposta = await _cliente.GetAsync($"{Base}?ordenarPor=senha");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotNull((await LerProblema(resposta))["errors"]!["ordenarPor"]);
    }

    [Fact]
    public async Task Combinacao_de_dois_filtros()
    {
        var pagina = await _cliente.GetFromJsonAsync<PaginaTarefas>($"{Base}?situacao=Pendente&prioridade=Alta", Json.Opcoes);

        Assert.NotEmpty(pagina!.Itens);
        Assert.All(pagina.Itens, t =>
        {
            Assert.Equal(SituacaoTarefa.Pendente, t.Situacao);
            Assert.Equal(PrioridadeTarefa.Alta, t.Prioridade);
        });
    }

    // ---------- CRUD e Problem Details ----------

    [Fact]
    public async Task Post_valido_retorna_201_com_location_e_get_encontra()
    {
        var resposta = await _cliente.PostAsJsonAsync(Base, new TarefaCriacao("Criada pela API v1"), Json.Opcoes);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var obtida = await _cliente.GetAsync(resposta.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, obtida.StatusCode);
    }

    [Fact]
    public async Task Post_invalido_retorna_400_problem_details_com_erros_por_campo()
    {
        var resposta = await _cliente.PostAsJsonAsync(Base, new { titulo = "", prioridade = "Alta" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await LerProblema(resposta);
        Assert.NotNull(problema["errors"]!["titulo"]);
        Assert.NotNull(problema["correlationId"]);
    }

    [Fact]
    public async Task Recurso_inexistente_retorna_404_problem_details()
    {
        var resposta = await _cliente.GetAsync($"{Base}/999999");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal(404, (int)(await LerProblema(resposta))["status"]!);
    }

    [Fact]
    public async Task Put_com_versao_antiga_retorna_409_sem_sobrescrever()
    {
        var tarefa = await CriarAsync("Conflito via API");
        var primeira = new TarefaAtualizacao("Primeira edição", null, PrioridadeTarefa.Media, null, SituacaoTarefa.Pendente, tarefa.Versao);
        var segunda = primeira with { Titulo = "Segunda edição (versão antiga)" };

        var ok = await _cliente.PutAsJsonAsync($"{Base}/{tarefa.Id}", primeira, Json.Opcoes);
        var conflito = await _cliente.PutAsJsonAsync($"{Base}/{tarefa.Id}", segunda, Json.Opcoes);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        var problema = await LerProblema(conflito);
        Assert.NotNull(problema["versaoAtual"]);
        var atual = await _cliente.GetFromJsonAsync<TarefaDetalhe>($"{Base}/{tarefa.Id}", Json.Opcoes);
        Assert.Equal("Primeira edição", atual!.Titulo);
    }

    [Fact]
    public async Task Delete_retorna_204_e_depois_404()
    {
        var tarefa = await CriarAsync("Para excluir");

        Assert.Equal(HttpStatusCode.NoContent, (await _cliente.DeleteAsync($"{Base}/{tarefa.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.DeleteAsync($"{Base}/{tarefa.Id}")).StatusCode);
    }

    [Fact]
    public async Task Resposta_nao_expoe_a_entidade_persistida()
    {
        var json = await _cliente.GetStringAsync($"{Base}/1");

        Assert.DoesNotContain("eventos", json, StringComparison.OrdinalIgnoreCase); // navegação da entidade
        Assert.Contains("\"situacao\":\"", json);                                    // enum como texto
    }
}
