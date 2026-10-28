using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Dados.Configuracoes;

/// <summary>Mapeamento da entidade Tarefa via Fluent API.</summary>
public sealed class TarefaConfiguracao : IEntityTypeConfiguration<Tarefa>
{
    public void Configure(EntityTypeBuilder<Tarefa> tarefa)
    {
        tarefa.ToTable("Tarefas");
        tarefa.HasKey(t => t.Id);

        tarefa.Property(t => t.Titulo).IsRequired().HasMaxLength(100);
        tarefa.Property(t => t.Descricao).HasMaxLength(500);

        // Enums gravados como texto: o banco fica legível e não depende da ordem dos membros.
        tarefa.Property(t => t.Situacao).HasConversion<string>().HasMaxLength(20).IsRequired();
        tarefa.Property(t => t.Prioridade).HasConversion<string>().HasMaxLength(20).IsRequired();

        tarefa.Property(t => t.CriadaEm).IsRequired();

        // rowversion: o SQL Server gera um novo valor a cada UPDATE e o EF Core o usa
        // na cláusula WHERE para detectar alteração concorrente.
        tarefa.Property(t => t.Versao).IsRowVersion();

        // Índice que atende à listagem por situação e ao painel de vencidas.
        tarefa.HasIndex(t => new { t.Situacao, t.Prazo }).HasDatabaseName("IX_Tarefas_Situacao_Prazo");

        // Extensão (2ª migration): índice no título, usado pela validação de título único.
        tarefa.HasIndex(t => t.Titulo).HasDatabaseName("IX_Tarefas_Titulo");

        tarefa.HasMany(t => t.Eventos)
            .WithOne()
            .HasForeignKey(e => e.TarefaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Mapeamento da entidade EventoAuditoria.</summary>
public sealed class EventoAuditoriaConfiguracao : IEntityTypeConfiguration<EventoAuditoria>
{
    public void Configure(EntityTypeBuilder<EventoAuditoria> evento)
    {
        evento.ToTable("EventosAuditoria");
        evento.HasKey(e => e.Id);
        evento.Property(e => e.Acao).IsRequired().HasMaxLength(50);
        evento.Property(e => e.Detalhe).HasMaxLength(200);
        evento.HasIndex(e => new { e.TarefaId, e.OcorridoEm });
    }
}
