using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Observabilidade;

namespace PortalTarefas.Web.Servicos.Painel;

/// <summary>Uma linha do painel: tarefas em aberto agrupadas por prioridade.</summary>
public sealed record LinhaPainel(PrioridadeTarefa Prioridade, int Abertas, int Vencidas, DateOnly? ProximoPrazo);

/// <summary>Mesma consulta de leitura, implementada com EF Core e com Dapper, para comparação.</summary>
public interface IConsultaPainel
{
    string Fonte { get; }
    Task<IReadOnlyList<LinhaPainel>> ObterAsync(DateOnly hoje, CancellationToken cancelamento = default);
}

/// <summary>
/// Versão EF Core: LINQ com projeção (só as colunas necessárias) e AsNoTracking
/// (as linhas não serão alteradas, então o ChangeTracker não precisa acompanhá-las).
/// </summary>
public sealed class ConsultaPainelEf(PortalTarefasDbContext db, Telemetria telemetria) : IConsultaPainel
{
    public string Fonte => "EF Core";

    public async Task<IReadOnlyList<LinhaPainel>> ObterAsync(DateOnly hoje, CancellationToken cancelamento = default)
    {
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var linhas = await db.Tarefas
            .AsNoTracking()
            .Where(t => t.Situacao != SituacaoTarefa.Concluida)
            .GroupBy(t => t.Prioridade)
            .Select(g => new LinhaPainel(
                g.Key,
                g.Count(),
                g.Count(t => t.Prazo < hoje),
                g.Where(t => t.Prazo >= hoje).Min(t => t.Prazo)))
            .ToListAsync(cancelamento);

        telemetria.PainelConsultado("ef", cronometro.Elapsed.TotalMilliseconds); // métrica técnica (Roteiro 7)
        return linhas.OrderByDescending(l => l.Prioridade).ToList();
    }
}

/// <summary>
/// Versão Dapper: SQL escrito à mão, com PARÂMETROS (@Hoje, @Concluida). Valores nunca
/// são interpolados no texto do comando, o que evita SQL injection.
/// </summary>
public sealed class ConsultaPainelDapper(IConfiguration configuracao, Telemetria telemetria) : IConsultaPainel
{
    private const string Sql = """
        SELECT  Prioridade,
                COUNT(*)                                          AS Abertas,
                SUM(CASE WHEN Prazo < @Hoje THEN 1 ELSE 0 END)    AS Vencidas,
                MIN(CASE WHEN Prazo >= @Hoje THEN Prazo END)      AS ProximoPrazo
        FROM    Tarefas
        WHERE   Situacao <> @Concluida
        GROUP BY Prioridade
        """;

    public string Fonte => "Dapper";

    public async Task<IReadOnlyList<LinhaPainel>> ObterAsync(DateOnly hoje, CancellationToken cancelamento = default)
    {
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        await using var conexao = new SqlConnection(configuracao.GetConnectionString("PortalTarefas"));
        var comando = new CommandDefinition(Sql,
            new { Hoje = hoje.ToDateTime(TimeOnly.MinValue), Concluida = nameof(SituacaoTarefa.Concluida) },
            cancellationToken: cancelamento);

        var linhas = await conexao.QueryAsync<LinhaSql>(comando);
        telemetria.PainelConsultado("dapper", cronometro.Elapsed.TotalMilliseconds);

        return linhas
            .Select(l => new LinhaPainel(
                Enum.Parse<PrioridadeTarefa>(l.Prioridade),
                l.Abertas,
                l.Vencidas,
                l.ProximoPrazo is null ? null : DateOnly.FromDateTime(l.ProximoPrazo.Value)))
            .OrderByDescending(l => l.Prioridade)
            .ToList();
    }

    // Tipo intermediário que espelha as colunas do SELECT.
    private sealed record LinhaSql(string Prioridade, int Abertas, int Vencidas, DateTime? ProximoPrazo);
}
