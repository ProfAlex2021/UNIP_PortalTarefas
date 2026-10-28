using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Servicos;

/// <summary>Implementação assíncrona de <see cref="ITarefaService"/> baseada em EF Core.</summary>
public sealed class TarefaServiceEf(
    PortalTarefasDbContext db,
    TimeProvider relogio,
    ILogger<TarefaServiceEf> logger) : ITarefaService
{
    public async Task<IReadOnlyList<Tarefa>> ListarAsync(FiltroTarefas filtro, CancellationToken cancelamento = default)
    {
        // Leitura sem rastreamento: as entidades não serão alteradas nesta requisição.
        var consulta = db.Tarefas.AsNoTracking();

        if (filtro.Situacao is not null)
            consulta = consulta.Where(t => t.Situacao == filtro.Situacao);
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
            consulta = consulta.Where(t => t.Titulo.Contains(filtro.Busca)); // vira LIKE com parâmetro

        return await consulta.OrderBy(t => t.Id).ToListAsync(cancelamento);
    }

    public Task<Tarefa?> ObterAsync(int id, CancellationToken cancelamento = default) =>
        db.Tarefas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancelamento);

    public async Task<Tarefa> CriarAsync(Tarefa tarefa, CancellationToken cancelamento = default)
    {
        tarefa.Id = 0;
        tarefa.CriadaEm = relogio.GetUtcNow();
        tarefa.Eventos.Add(NovoEvento(0, "Criada", null));
        db.Tarefas.Add(tarefa);
        await db.SaveChangesAsync(cancelamento);
        return tarefa;
    }

    public async Task<ResultadoAtualizacao> AtualizarAsync(Tarefa alterada, CancellationToken cancelamento = default)
    {
        var entidade = await db.Tarefas.FirstOrDefaultAsync(t => t.Id == alterada.Id, cancelamento);
        if (entidade is null)
            return new(ResultadoOperacao.NaoEncontrada);

        // Informa ao EF Core qual versão o usuário viu. O UPDATE terá "WHERE Id = @id AND Versao = @versao";
        // se outra pessoa salvou antes, nenhuma linha é afetada e ocorre DbUpdateConcurrencyException.
        db.Entry(entidade).Property(t => t.Versao).OriginalValue = alterada.Versao;

        entidade.Titulo = alterada.Titulo;
        entidade.Descricao = alterada.Descricao;
        entidade.Prioridade = alterada.Prioridade;
        entidade.Prazo = alterada.Prazo;
        entidade.Situacao = alterada.Situacao;
        entidade.AtualizadaEm = relogio.GetUtcNow();
        db.EventosAuditoria.Add(NovoEvento(entidade.Id, "Editada", null));

        try
        {
            await db.SaveChangesAsync(cancelamento);
            return new(ResultadoOperacao.Sucesso, entidade);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Estratégia explícita: o banco prevalece. Nada é sobrescrito; devolvemos os
            // valores atuais para o usuário revisar e decidir se salva novamente.
            var valoresNoBanco = await ex.Entries.Single(e => e.Entity is Tarefa).GetDatabaseValuesAsync(cancelamento);
            logger.LogWarning("Conflito de concorrência ao editar a tarefa {TarefaId}", alterada.Id);

            return valoresNoBanco is null
                ? new(ResultadoOperacao.NaoEncontrada)
                : new(ResultadoOperacao.Conflito, (Tarefa)valoresNoBanco.ToObject());
        }
    }

    public async Task<ResultadoOperacao> AlterarSituacaoAsync(int id, SituacaoTarefa novaSituacao, string? observacao,
        CancellationToken cancelamento = default)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(cancelamento);
        try
        {
            var tarefa = await db.Tarefas.FirstOrDefaultAsync(t => t.Id == id, cancelamento);
            if (tarefa is null)
                return ResultadoOperacao.NaoEncontrada;

            var anterior = tarefa.Situacao;
            tarefa.Situacao = novaSituacao;
            tarefa.AtualizadaEm = relogio.GetUtcNow();
            await db.SaveChangesAsync(cancelamento);            // 1ª gravação: a tarefa

            var detalhe = $"{anterior} -> {novaSituacao}" + (string.IsNullOrWhiteSpace(observacao) ? "" : $": {observacao}");
            db.EventosAuditoria.Add(NovoEvento(id, "SituacaoAlterada", detalhe));
            await db.SaveChangesAsync(cancelamento);            // 2ª gravação: a auditoria

            await transacao.CommitAsync(cancelamento);          // só agora as duas ficam visíveis
            return ResultadoOperacao.Sucesso;
        }
        catch (Exception ex) when (ex is DbUpdateException or OperationCanceledException)
        {
            await transacao.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            logger.LogWarning("Transação de alteração de situação da tarefa {TarefaId} desfeita", id);
            throw;
        }
    }

    public Task<bool> ExisteTituloEmAbertoAsync(string titulo, int? ignorarId, CancellationToken cancelamento = default)
    {
        var normalizado = titulo.Trim();
        return db.Tarefas.AnyAsync(t =>
            t.Id != ignorarId &&
            t.Situacao != SituacaoTarefa.Concluida &&
            t.Titulo == normalizado, cancelamento); // collation padrão do SQL Server ignora maiúsculas
    }

    public async Task<IReadOnlyList<Tarefa>> ListarVencidasAsync(DateOnly hoje, CancellationToken cancelamento = default) =>
        await db.Tarefas.AsNoTracking()
            .Where(t => t.Prazo < hoje && t.Situacao != SituacaoTarefa.Concluida)
            .OrderBy(t => t.Prazo)
            .ToListAsync(cancelamento);

    public async Task<IReadOnlyList<EventoAuditoria>> ListarEventosAsync(int tarefaId, CancellationToken cancelamento = default) =>
        await db.EventosAuditoria.AsNoTracking()
            .Where(e => e.TarefaId == tarefaId)
            .OrderByDescending(e => e.OcorridoEm)
            .ToListAsync(cancelamento);

    private EventoAuditoria NovoEvento(int tarefaId, string acao, string? detalhe) => new()
    {
        TarefaId = tarefaId,
        Acao = acao,
        Detalhe = detalhe,
        OcorridoEm = relogio.GetUtcNow()
    };
}
