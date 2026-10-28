using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Dados;

/// <summary>
/// Contexto do EF Core. É registrado com AddDbContextPool, por isso NÃO guarda estado
/// próprio (campos, serviços scoped injetados no construtor etc.): ao voltar ao pool,
/// o EF Core limpa o rastreamento, mas qualquer estado extra seria compartilhado
/// entre requisições diferentes.
/// </summary>
public class PortalTarefasDbContext(DbContextOptions<PortalTarefasDbContext> opcoes) : DbContext(opcoes)
{
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo);
        // Aplica todas as classes IEntityTypeConfiguration<T> deste assembly (pasta Configuracoes).
        modelo.ApplyConfigurationsFromAssembly(typeof(PortalTarefasDbContext).Assembly);
    }
}
