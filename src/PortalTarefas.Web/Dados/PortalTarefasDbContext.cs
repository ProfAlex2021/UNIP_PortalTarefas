using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Dominio;
using PortalTarefas.Web.Seguranca;

namespace PortalTarefas.Web.Dados;

/// <summary>
/// Contexto do EF Core. É registrado com AddDbContextPool, por isso NÃO guarda estado
/// próprio (campos, serviços scoped injetados no construtor etc.): ao voltar ao pool,
/// o EF Core limpa o rastreamento, mas qualquer estado extra seria compartilhado
/// entre requisições diferentes.
/// Desde o Roteiro 6 herda de IdentityDbContext, que acrescenta as tabelas do
/// ASP.NET Core Identity (usuários, papéis, claims, logins e tokens).
/// </summary>
public class PortalTarefasDbContext(DbContextOptions<PortalTarefasDbContext> opcoes)
    : IdentityDbContext<Usuario>(opcoes)
{
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo); // mapeamento das tabelas do Identity
        // Aplica todas as classes IEntityTypeConfiguration<T> deste assembly (pasta Configuracoes).
        modelo.ApplyConfigurationsFromAssembly(typeof(PortalTarefasDbContext).Assembly);
    }
}
