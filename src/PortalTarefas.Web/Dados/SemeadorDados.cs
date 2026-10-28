using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Dados;

/// <summary>
/// Seed idempotente: cada tarefa de exemplo só é inserida se ainda não existir
/// uma tarefa com o mesmo título. Executar várias vezes não duplica dados.
/// </summary>
public static class SemeadorDados
{
    public static async Task<int> ExecutarAsync(PortalTarefasDbContext db, TimeProvider relogio, CancellationToken cancelamento = default)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        var agora = relogio.GetUtcNow();

        Tarefa[] exemplos =
        [
            new() { Titulo = "Configurar ambiente .NET 10", Descricao = "Instalar SDK e Visual Studio 2026",
                    Situacao = SituacaoTarefa.Concluida, Prioridade = PrioridadeTarefa.Alta, Prazo = hoje.AddDays(-10) },
            new() { Titulo = "Estudar pipeline HTTP", Descricao = "Ler a Unidade I",
                    Situacao = SituacaoTarefa.EmAndamento, Prioridade = PrioridadeTarefa.Media, Prazo = hoje.AddDays(5) },
            new() { Titulo = "Entregar relatório do Roteiro 1",
                    Situacao = SituacaoTarefa.Pendente, Prioridade = PrioridadeTarefa.Alta, Prazo = hoje.AddDays(-2) },
            new() { Titulo = "Revisar migrations do EF Core", Descricao = "Conferir o SQL gerado antes de aplicar",
                    Situacao = SituacaoTarefa.Pendente, Prioridade = PrioridadeTarefa.Media, Prazo = hoje.AddDays(7) },
            new() { Titulo = "Comparar EF Core e Dapper",
                    Situacao = SituacaoTarefa.Pendente, Prioridade = PrioridadeTarefa.Baixa },
        ];

        var titulosExistentes = await db.Tarefas
            .Select(t => t.Titulo)
            .ToListAsync(cancelamento);

        var novas = exemplos.Where(e => !titulosExistentes.Contains(e.Titulo)).ToList();
        foreach (var tarefa in novas)
            tarefa.CriadaEm = agora;

        db.Tarefas.AddRange(novas);
        await db.SaveChangesAsync(cancelamento);
        return novas.Count;
    }
}
