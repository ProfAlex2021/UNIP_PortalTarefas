using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortalTarefas.Web.Dados;

namespace PortalTarefas.Tests.Apoio;

/// <summary>
/// Sobe a aplicação em memória apontando para um banco de dados EXCLUSIVO, com nome
/// aleatório, criado pelas migrations na inicialização e apagado ao final.
/// Assim os testes não dependem do banco de desenvolvimento nem uns dos outros.
///
/// O servidor SQL vem da variável de ambiente PORTALTAREFAS_SQL_TESTES (usada no CI);
/// sem ela, usa o SQL Server LocalDB instalado com o Visual Studio.
/// </summary>
public class FabricaPortal : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServidorPadrao = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True";

    public string ConnectionString { get; } = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("PORTALTAREFAS_SQL_TESTES") ?? ServidorPadrao)
    {
        InitialCatalog = $"PortalTarefas_Testes_{Guid.NewGuid():N}"
    }.ConnectionString;

    /// <summary>Configurações extras que classes derivadas podem acrescentar.</summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ConfiguracoesAdicionais() => [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PortalTarefas"] = ConnectionString,
                ["Banco:AplicarMigrationsNaInicializacao"] = "true",
            }.Concat(ConfiguracoesAdicionais()));
        });
    }

    public HttpClient CriarClienteHttps() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        SqlConnection.ClearAllPools();
        var opcoes = new DbContextOptionsBuilder<PortalTarefasDbContext>().UseSqlServer(ConnectionString).Options;
        await using var db = new PortalTarefasDbContext(opcoes);
        await db.Database.EnsureDeletedAsync();
    }
}
