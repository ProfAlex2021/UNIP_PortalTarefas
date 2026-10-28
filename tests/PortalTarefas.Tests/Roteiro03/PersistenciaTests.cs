using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortalTarefas.Tests.Apoio;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.Servicos.Painel;

namespace PortalTarefas.Tests.Roteiro03;

/// <summary>
/// Testes de integração com banco isolado (extensão do Roteiro 3): transação,
/// rollback, concorrência, seed e comparação EF Core x Dapper.
/// </summary>
public class PersistenciaTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    private AsyncServiceScope NovoEscopo() => fabrica.Services.CreateAsyncScope();

    private async Task<Tarefa> CriarTarefaAsync(string titulo)
    {
        await using var escopo = NovoEscopo();
        var servico = escopo.ServiceProvider.GetRequiredService<ITarefaService>();
        return await servico.CriarAsync(new Tarefa { Titulo = titulo, Prioridade = PrioridadeTarefa.Baixa });
    }

    [Fact]
    public async Task Seed_e_idempotente_ao_executar_novamente()
    {
        await using var escopo = NovoEscopo();
        var db = escopo.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();
        var antes = await db.Tarefas.CountAsync();

        var inseridas = await SemeadorDados.ExecutarAsync(db, TimeProvider.System);

        Assert.Equal(0, inseridas);
        Assert.Equal(antes, await db.Tarefas.CountAsync());
    }

    [Fact]
    public async Task Alteracao_de_situacao_grava_tarefa_e_auditoria_juntas()
    {
        var tarefa = await CriarTarefaAsync("Transação com sucesso");

        await using (var escopo = NovoEscopo())
        {
            var servico = escopo.ServiceProvider.GetRequiredService<ITarefaService>();
            var resultado = await servico.AlterarSituacaoAsync(tarefa.Id, SituacaoTarefa.EmAndamento, "iniciada no teste");
            Assert.Equal(ResultadoOperacao.Sucesso, resultado);
        }

        await using var verificacao = NovoEscopo();
        var leitura = verificacao.ServiceProvider.GetRequiredService<ITarefaService>();
        Assert.Equal(SituacaoTarefa.EmAndamento, (await leitura.ObterAsync(tarefa.Id))!.Situacao);
        Assert.Contains(await leitura.ListarEventosAsync(tarefa.Id), e => e.Acao == "SituacaoAlterada");
    }

    [Fact]
    public async Task Falha_na_auditoria_desfaz_a_alteracao_da_tarefa()
    {
        var tarefa = await CriarTarefaAsync("Transação com rollback");
        var observacaoLongaDemais = new string('x', 250); // excede o limite de 200 da coluna Detalhe

        await using (var escopo = NovoEscopo())
        {
            var servico = escopo.ServiceProvider.GetRequiredService<ITarefaService>();
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                servico.AlterarSituacaoAsync(tarefa.Id, SituacaoTarefa.Concluida, observacaoLongaDemais));
        }

        await using var verificacao = NovoEscopo();
        var leitura = verificacao.ServiceProvider.GetRequiredService<ITarefaService>();
        Assert.Equal(SituacaoTarefa.Pendente, (await leitura.ObterAsync(tarefa.Id))!.Situacao); // 1ª gravação desfeita
        Assert.DoesNotContain(await leitura.ListarEventosAsync(tarefa.Id), e => e.Acao == "SituacaoAlterada");
    }

    [Fact]
    public async Task Duas_edicoes_da_mesma_versao_geram_conflito_sem_sobrescrever()
    {
        var original = await CriarTarefaAsync("Concorrência original");

        // Sessões A e B abrem a mesma tarefa (mesma versão).
        await using var sessaoA = NovoEscopo();
        await using var sessaoB = NovoEscopo();
        var servicoA = sessaoA.ServiceProvider.GetRequiredService<ITarefaService>();
        var servicoB = sessaoB.ServiceProvider.GetRequiredService<ITarefaService>();
        var vistaPorA = (await servicoA.ObterAsync(original.Id))!;
        var vistaPorB = (await servicoB.ObterAsync(original.Id))!;

        vistaPorA.Titulo = "Alterada pela sessão A";
        var resultadoA = await servicoA.AtualizarAsync(vistaPorA);

        vistaPorB.Titulo = "Alterada pela sessão B";
        var resultadoB = await servicoB.AtualizarAsync(vistaPorB);

        Assert.Equal(ResultadoOperacao.Sucesso, resultadoA.Resultado);
        Assert.Equal(ResultadoOperacao.Conflito, resultadoB.Resultado);
        Assert.Equal("Alterada pela sessão A", resultadoB.Atual!.Titulo); // B recebe o valor atual

        await using var verificacao = NovoEscopo();
        var noBanco = await verificacao.ServiceProvider.GetRequiredService<ITarefaService>().ObterAsync(original.Id);
        Assert.Equal("Alterada pela sessão A", noBanco!.Titulo); // B não sobrescreveu A
    }

    [Fact]
    public async Task Consulta_de_painel_ef_e_dapper_retornam_o_mesmo_resultado()
    {
        await using var escopo = NovoEscopo();
        var consultas = escopo.ServiceProvider.GetServices<IConsultaPainel>().ToList();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        var ef = await consultas.Single(c => c.Fonte == "EF Core").ObterAsync(hoje);
        var dapper = await consultas.Single(c => c.Fonte == "Dapper").ObterAsync(hoje);

        Assert.NotEmpty(ef);
        Assert.Equal(ef, dapper);
    }

    [Fact]
    public async Task Listagem_usa_leitura_sem_rastreamento()
    {
        await using var escopo = NovoEscopo();
        var servico = escopo.ServiceProvider.GetRequiredService<ITarefaService>();
        var db = escopo.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();

        var tarefas = await servico.ListarAsync(new FiltroTarefas());

        Assert.NotEmpty(tarefas);
        Assert.Empty(db.ChangeTracker.Entries()); // nada ficou sendo rastreado
    }
}

/// <summary>Instalação limpa em banco próprio: o esquema vem das migrations e só há o seed.</summary>
public class InstalacaoLimpaTests(FabricaPortal fabrica) : IClassFixture<FabricaPortal>
{
    [Fact]
    public async Task Banco_novo_recebe_todas_as_migrations_e_somente_o_seed()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(5, await db.Tarefas.CountAsync());
    }
}
