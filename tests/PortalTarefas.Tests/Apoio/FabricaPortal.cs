using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Resiliencia;

namespace PortalTarefas.Tests.Apoio;

/// <summary>Usuários de teste criados pelo seed (mesmos e-mails de appsettings.Development.json).</summary>
public enum Perfil
{
    Administrador = 0, // equipe Alfa, papel Administrador
    UsuarioAlfa = 1,   // equipe Alfa, papel Usuario
    UsuarioBeta = 2    // equipe Beta, papel Usuario
}

/// <summary>
/// Sobe a aplicação em memória apontando para um banco de dados EXCLUSIVO, com nome
/// aleatório, criado pelas migrations na inicialização e apagado ao final.
/// Assim os testes não dependem do banco de desenvolvimento nem uns dos outros.
///
/// O servidor SQL vem da variável de ambiente PORTALTAREFAS_SQL_TESTES (usada no CI);
/// sem ela, usa o SQL Server LocalDB instalado com o Visual Studio.
/// As senhas dos usuários de teste são geradas a cada execução (nada fixo no repositório).
/// </summary>
public class FabricaPortal : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServidorPadrao = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True";

    public static readonly IReadOnlyDictionary<Perfil, string> Emails = new Dictionary<Perfil, string>
    {
        [Perfil.Administrador] = "admin@portaltarefas.local",
        [Perfil.UsuarioAlfa] = "ana@portaltarefas.local",
        [Perfil.UsuarioBeta] = "bruno@portaltarefas.local",
    };

    private readonly Dictionary<Perfil, string> _senhas = Enum.GetValues<Perfil>()
        .ToDictionary(p => p, _ => $"Teste{Guid.NewGuid():N}"[..20]);

    public string ConnectionString { get; } = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("PORTALTAREFAS_SQL_TESTES") ?? ServidorPadrao)
    {
        InitialCatalog = $"PortalTarefas_Testes_{Guid.NewGuid():N}"
    }.ConnectionString;

    public string SenhaDe(Perfil perfil) => _senhas[perfil];

    /// <summary>Configurações extras que classes derivadas podem acrescentar.</summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ConfiguracoesAdicionais() => [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            var valores = new Dictionary<string, string?>
            {
                ["ConnectionStrings:PortalTarefas"] = ConnectionString,
                ["Banco:AplicarMigrationsNaInicializacao"] = "true",
                // Roteiro 7: dependência simulada servida pelo próprio TestServer e limite de login folgado
                // (vários testes fazem login). O teste de rate limiting usa uma fábrica com limite baixo.
                ["Simulacao:Habilitada"] = "true",
                ["Dependencias:Calendario:UrlBase"] = "https://localhost/simulacao/calendario/",
                ["Limites:Login:PermitidasPorJanela"] = "1000",
            };
            foreach (var (perfil, senha) in _senhas)
                valores[$"SeedUsuarios:{(int)perfil}:Senha"] = senha;

            configuracao.AddInMemoryCollection(valores);
            configuracao.AddInMemoryCollection(ConfiguracoesAdicionais()); // camada posterior: sobrescreve
        });

        // Não existe servidor HTTP real nos testes: as chamadas à dependência simulada vão
        // para o servidor em memória. O pipeline de resiliência continua o mesmo.
        builder.ConfigureTestServices(servicos =>
        {
            servicos.AddHttpClient("calendario-saude").ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler());
            servicos.AddHttpClient<ClienteCalendario>().ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler());
        });
    }

    public HttpClient CriarClienteHttps() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>
    /// Cliente que já fez login pela API (o cookie de sessão fica guardado no cliente)
    /// e envia o cabeçalho anti-CSRF exigido nas operações que alteram dados.
    /// </summary>
    public async Task<HttpClient> CriarClienteAutenticadoAsync(Perfil perfil = Perfil.Administrador)
    {
        var cliente = CriarClienteHttps();
        cliente.DefaultRequestHeaders.Add("X-Requested-With", "PortalTarefas");
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email = Emails[perfil], senha = SenhaDe(perfil) });
        resposta.EnsureSuccessStatusCode();
        return cliente;
    }

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
