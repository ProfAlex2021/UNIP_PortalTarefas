# Roteiro 3 — EF Core, migrations, seed, pooling, transações, concorrência e acesso leve

## Pré-requisitos

- SQL Server LocalDB (instalado com a carga de trabalho ASP.NET do Visual Studio 2026). Verifique com `sqllocaldb info`.
- Pacotes: `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12, `Microsoft.EntityFrameworkCore.Design` 10.0.12, `Dapper` 2.1.66.
- Ferramenta local `dotnet-ef` 10.0.12 (manifesto `dotnet-tools.json`): `dotnet tool restore`.

## Connection string em User Secrets (nunca no repositório)

```bash
dotnet user-secrets set "ConnectionStrings:PortalTarefas" "Server=(localdb)\MSSQLLocalDB;Database=PortalTarefas;Trusted_Connection=True;TrustServerCertificate=True" --project src/PortalTarefas.Web
```

No Visual Studio: botão direito no projeto **PortalTarefas.Web → Gerenciar Segredos do Usuário**.
Sem essa configuração, a aplicação falha ao resolver o `DbContext` com uma mensagem que explica o comando acima,
sem exibir nenhum valor sensível.

## Migrations

Pelo **Console do Gerenciador de Pacotes** do Visual Studio (projeto padrão: PortalTarefas.Web):

```powershell
Add-Migration CriacaoInicial -OutputDir Dados/Migrations
Script-Migration -Idempotent        # revisar o SQL
Update-Database
```

Ou pelas ferramentas `dotnet ef`:

```bash
dotnet tool restore
dotnet ef migrations add CriacaoInicial --project src/PortalTarefas.Web --output-dir Dados/Migrations
dotnet ef migrations script --idempotent --project src/PortalTarefas.Web -o docs/sql/01-CriacaoInicial.sql
dotnet ef database update --project src/PortalTarefas.Web
```

Em desenvolvimento, `Banco:AplicarMigrationsNaInicializacao = true` (em `appsettings.Development.json`) faz a
aplicação aplicar as migrations pendentes e o seed ao iniciar. Em produção essa opção fica desligada.

SQL gerado e revisado: [`docs/sql/01-CriacaoInicial.sql`](../sql/01-CriacaoInicial.sql) e
[`docs/sql/02-IndiceTituloTarefas.sql`](../sql/02-IndiceTituloTarefas.sql) (extensão: índice adicionado por migration).

Para recriar o banco do zero: `dotnet ef database drop -f --project src/PortalTarefas.Web` e executar a aplicação.

## Arquivos produzidos

| Arquivo | Papel |
|---|---|
| `Dominio/Tarefa.cs` | Entidades `Tarefa` (com `Versao` rowversion) e `EventoAuditoria` |
| `Dados/PortalTarefasDbContext.cs` | Contexto sem estado próprio (compatível com pooling) |
| `Dados/Configuracoes/TarefaConfiguracao.cs` | Fluent API: chave, limites, obrigatoriedade, enums como texto, índices, rowversion |
| `Dados/Migrations/*` | `CriacaoInicial` e `IndiceTituloTarefas` |
| `Dados/SemeadorDados.cs` | Seed idempotente (insere só o que não existe) |
| `Servicos/TarefaServiceEf.cs` | Serviço assíncrono com EF Core, transação explícita e tratamento de concorrência |
| `Servicos/Painel/ConsultasPainel.cs` | Mesma consulta em EF Core (projeção + AsNoTracking) e Dapper (SQL parametrizado) |
| `Controllers/PainelController.cs` + view | Mostra as duas consultas lado a lado com o tempo medido |
| `tests/.../Apoio/FabricaPortal.cs` | **Extensão:** cada classe de teste usa um banco isolado, criado e apagado automaticamente |
| `tests/.../Roteiro03/PersistenciaTests.cs` | **Extensão:** testes de integração com banco isolado |

## Pooling

`AddDbContextPool` reaproveita instâncias do `PortalTarefasDbContext`. Antes de devolver ao pool o EF Core
limpa o rastreamento, mas qualquer campo extra do contexto seria compartilhado entre requisições.
Por isso o contexto só recebe `DbContextOptions` no construtor. O pooling de **conexões** é feito pelo
`Microsoft.Data.SqlClient`, independentemente disso.

## Transações

| Operação | Tipo | Comportamento |
|---|---|---|
| `AtualizarAsync` (edição) | Implícita: um único `SaveChanges` grava a tarefa e o evento | Tudo ou nada; o EF Core abre e confirma a transação |
| `AlterarSituacaoAsync` | Explícita: `BeginTransactionAsync` + dois `SaveChanges` + `CommitAsync` | Se a 2ª gravação falhar, `RollbackAsync` desfaz a 1ª |

O teste `Falha_na_auditoria_desfaz_a_alteracao_da_tarefa` provoca o rollback de propósito com uma observação
maior que a coluna `Detalhe` (200). Pela interface, a observação é limitada a 150 caracteres.

## Concorrência otimista

- `Versao` é `rowversion` no SQL Server: o banco muda o valor a cada UPDATE.
- A edição envia a versão em campo oculto (Base64). O serviço define essa versão como *valor original*, e o
  `UPDATE ... WHERE Id = @p AND Versao = @v` não afeta linhas se alguém salvou antes → `DbUpdateConcurrencyException`.
- **Estratégia:** o banco prevalece. Nada é sobrescrito; a tela mostra os valores atuais e a nova versão,
  e o usuário decide se salva novamente.

**Como reproduzir com duas sessões:** abra `/Tarefas/Edit/2` em duas abas, salve na primeira e depois na segunda.
A segunda mostra a mensagem de conflito e o quadro "Valores atuais no banco".

## EF Core x Dapper (consulta de painel)

| Critério | EF Core | Dapper |
|---|---|---|
| Quantidade de código | LINQ curto, sem SQL | SQL + tipo intermediário + conversões |
| Rastreamento | Desligado com `AsNoTracking` (projeção já não rastreia) | Não existe |
| Clareza | Expressa a intenção em C#, verificada pelo compilador | SQL explícito, fácil de copiar para o SSMS |
| Controle do SQL | Gerado pelo provedor (visível nos logs) | Total |
| Parâmetros | Automáticos | `@Hoje`, `@Concluida` via objeto anônimo — nunca interpolação |
| Cenário de uso | CRUD e a maioria das consultas | Relatórios, consultas muito específicas ou ajustadas à mão |

A página `/Painel` exibe as duas versões e o tempo de cada uma **nesta máquina**, após aquecimento.
Esses números são uma medição local e não permitem conclusão geral de desempenho.

## Testes obrigatórios (relatório, item 4)

| Teste | Onde |
|---|---|
| Banco criado a partir das migrations em ambiente limpo | `InstalacaoLimpaTests` |
| Seed idempotente | `Seed_e_idempotente_ao_executar_novamente` |
| Transação com sucesso e rollback provocado | `Alteracao_de_situacao_*`, `Falha_na_auditoria_*` |
| Conflito com duas edições da mesma tarefa | `Duas_edicoes_da_mesma_versao_geram_conflito_sem_sobrescrever` |
| Consulta com projeção e sem rastreamento | `Consulta_de_painel_*`, `Listagem_usa_leitura_sem_rastreamento` |

Os testes usam LocalDB por padrão. Para outro servidor, defina `PORTALTAREFAS_SQL_TESTES`
(connection string sem o nome do banco; cada classe de teste cria o seu).
