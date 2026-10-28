using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Servicos;

namespace PortalTarefas.Web.Api.V1;

/// <summary>
/// Serviço de aplicação da API v1: traduz DTOs em operações de domínio.
/// Consultas paginadas usam o DbContext diretamente (filtro, ordenação e paginação
/// executados pelo banco); comandos passam pelo <see cref="ITarefaService"/>.
/// </summary>
public sealed class TarefasApiServico(PortalTarefasDbContext db, ITarefaService servico, TimeProvider relogio)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().Date);

    public async Task<PaginaTarefas> ListarAsync(ParametrosConsultaTarefas p, CancellationToken cancelamento)
    {
        var consulta = db.Tarefas.AsNoTracking();

        if (p.Situacao is not null) consulta = consulta.Where(t => t.Situacao == p.Situacao);
        if (p.Prioridade is not null) consulta = consulta.Where(t => t.Prioridade == p.Prioridade);
        if (!string.IsNullOrWhiteSpace(p.Busca)) consulta = consulta.Where(t => t.Titulo.Contains(p.Busca));

        var total = await consulta.CountAsync(cancelamento);

        var descendente = string.Equals(p.Direcao, "desc", StringComparison.OrdinalIgnoreCase);
        var ordenada = Ordenar(consulta, p.OrdenarPor, descendente);

        var hoje = Hoje;
        var itens = await ordenada
            .Skip((p.Pagina - 1) * p.Tamanho)
            .Take(p.Tamanho)
            .Select(t => new TarefaResumo(
                t.Id, t.Titulo, t.Situacao, t.Prioridade, t.Prazo,
                t.Prazo < hoje && t.Situacao != SituacaoTarefa.Concluida))
            .ToListAsync(cancelamento);

        var totalPaginas = (int)Math.Ceiling(total / (double)p.Tamanho);
        return new PaginaTarefas(itens, p.Pagina, p.Tamanho, total, totalPaginas);
    }

    public async Task<TarefaDetalhe?> ObterAsync(int id, CancellationToken cancelamento)
    {
        var tarefa = await servico.ObterAsync(id, cancelamento);
        return tarefa is null ? null : TarefaDetalhe.De(tarefa);
    }

    public Task<bool> TituloEmUsoAsync(string titulo, int? ignorarId, CancellationToken cancelamento) =>
        servico.ExisteTituloEmAbertoAsync(titulo, ignorarId, cancelamento);

    public async Task<TarefaDetalhe> CriarAsync(TarefaCriacao dto, CancellationToken cancelamento)
    {
        var criada = await servico.CriarAsync(new Tarefa
        {
            Titulo = dto.Titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim(),
            Prioridade = dto.Prioridade,
            Prazo = dto.Prazo
        }, cancelamento);
        return TarefaDetalhe.De(criada);
    }

    public async Task<(ResultadoOperacao Resultado, TarefaDetalhe? Tarefa)> AtualizarAsync(
        int id, TarefaAtualizacao dto, byte[] versao, CancellationToken cancelamento)
    {
        var resultado = await servico.AtualizarAsync(new Tarefa
        {
            Id = id,
            Titulo = dto.Titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim(),
            Prioridade = dto.Prioridade,
            Prazo = dto.Prazo,
            Situacao = dto.Situacao,
            Versao = versao
        }, cancelamento);

        return (resultado.Resultado, resultado.Atual is null ? null : TarefaDetalhe.De(resultado.Atual));
    }

    public Task<bool> ExcluirAsync(int id, CancellationToken cancelamento) => servico.ExcluirAsync(id, cancelamento);

    /// <summary>Ordenação por lista permitida: o texto do cliente nunca vira SQL.</summary>
    private static IQueryable<Tarefa> Ordenar(IQueryable<Tarefa> consulta, string? campo, bool descendente)
    {
        // Prioridade e situação são gravadas como texto; o CASE garante a ordem de negócio, não a alfabética.
        Expression<Func<Tarefa, int>> pesoPrioridade = t =>
            t.Prioridade == PrioridadeTarefa.Alta ? 3 : t.Prioridade == PrioridadeTarefa.Media ? 2 : 1;
        Expression<Func<Tarefa, int>> pesoSituacao = t =>
            t.Situacao == SituacaoTarefa.Concluida ? 3 : t.Situacao == SituacaoTarefa.EmAndamento ? 2 : 1;

        var ordenada = campo?.ToLowerInvariant() switch
        {
            "titulo" => Aplicar(consulta, t => t.Titulo, descendente),
            "prazo" => Aplicar(consulta, t => t.Prazo, descendente),
            "prioridade" => Aplicar(consulta, pesoPrioridade, descendente),
            "situacao" => Aplicar(consulta, pesoSituacao, descendente),
            "criadaem" => Aplicar(consulta, t => t.CriadaEm, descendente),
            _ => Aplicar(consulta, t => t.Id, descendente),
        };
        return ordenada.ThenBy(t => t.Id); // desempate estável para a paginação

        static IOrderedQueryable<Tarefa> Aplicar<TChave>(IQueryable<Tarefa> q, Expression<Func<Tarefa, TChave>> chave, bool desc) =>
            desc ? q.OrderByDescending(chave) : q.OrderBy(chave);
    }
}
