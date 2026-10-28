using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using PortalTarefas.Web.Dados;
using PortalTarefas.Web.Endpoints;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Infraestrutura;
using PortalTarefas.Web.Middlewares;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.Servicos.Painel;
using PortalTarefas.Web.Validacao;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Registro de serviços (contêiner de injeção de dependência)
// ---------------------------------------------------------------------------

// Problem Details (RFC 9457) como formato padrão de erro + tratador global.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorExcecoesGlobal>();

// Validação automática de Data Annotations nos parâmetros das Minimal APIs (.NET 10).
builder.Services.AddValidation();

// MVC com views (Roteiro 2). O antiforgery é validado automaticamente em todo POST de formulário.
builder.Services.AddControllersWithViews(opcoes =>
{
    opcoes.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddScoped<RegistrarExecucaoAcaoFilter>();

// Razor codifica caracteres fora do ASCII por padrão (á vira &#xE1;). Liberamos o Unicode
// para o HTML gerado ficar legível; a proteção contra XSS (<, >, ", &) continua ativa.
builder.Services.Configure<WebEncoderOptions>(opcoes =>
    opcoes.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Persistência (Roteiro 3). A connection string vem de User Secrets em desenvolvimento
// e de variáveis de ambiente nos demais ambientes; nunca do repositório.
// AddDbContextPool reaproveita instâncias do contexto — seguro porque o contexto não guarda estado próprio.
builder.Services.AddDbContextPool<PortalTarefasDbContext>((servicos, opcoes) =>
{
    var conexao = servicos.GetRequiredService<IConfiguration>().GetConnectionString("PortalTarefas");
    if (string.IsNullOrWhiteSpace(conexao))
        throw new InvalidOperationException(
            "Connection string 'PortalTarefas' não configurada. Em desenvolvimento, use: " +
            "dotnet user-secrets set \"ConnectionStrings:PortalTarefas\" \"<sua conexão>\" --project src/PortalTarefas.Web");
    opcoes.UseSqlServer(conexao);
});

// Serviços da aplicação. O relógio é injetado para facilitar testes de datas.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITarefaService, TarefaServiceEf>();
builder.Services.AddScoped<ValidadorTituloUnico>();
builder.Services.AddScoped<IConsultaPainel, ConsultaPainelEf>();
builder.Services.AddScoped<IConsultaPainel, ConsultaPainelDapper>();

// Política CORS nomeada: somente a origem do front-end local é aceita.
const string PoliticaCorsFrontend = "FrontendLocal";
var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy(PoliticaCorsFrontend, politica => politica
        .WithOrigins(origensPermitidas)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(CorrelacaoMiddleware.NomeCabecalho));
});

var app = builder.Build();

// Aplica migrations pendentes e executa o seed idempotente, quando habilitado por configuração
// (ligado em Development; em produção as migrations devem ser aplicadas por um passo controlado).
if (app.Configuration.GetValue<bool>("Banco:AplicarMigrationsNaInicializacao"))
{
    await using var escopo = app.Services.CreateAsyncScope();
    var db = escopo.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();
    await db.Database.MigrateAsync();
    var inseridas = await SemeadorDados.ExecutarAsync(db, TimeProvider.System);
    app.Logger.LogInformation("Banco atualizado; seed inseriu {Quantidade} tarefa(s)", inseridas);
}

// ---------------------------------------------------------------------------
// 2. Pipeline HTTP — a ORDEM dos middlewares define o comportamento.
// ---------------------------------------------------------------------------

// (1) Tratamento centralizado de exceções: primeiro, para envolver todo o restante.
app.UseExceptionHandler();
app.UseStatusCodePages();

// (2) HSTS apenas fora de desenvolvimento.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// (3) Redireciona HTTP -> HTTPS.
app.UseHttpsRedirection();

// (4) Identificador de correlação: todo log seguinte o carrega.
app.UseCorrelacao();

// (5) Middleware próprio que registra método, caminho, status e duração.
app.UseRegistroRequisicao();

// (6) Arquivos estáticos de wwwroot (encerra a requisição se o arquivo existir).
app.UseStaticFiles();

// (7) Roteamento: escolhe o endpoint que atende a requisição.
app.UseRouting();

// (8) CORS depois do roteamento e antes dos endpoints.
app.UseCors(PoliticaCorsFrontend);

// (9) Endpoints.
// Endpoints de API do Roteiro 1, mantidos em um grupo próprio (/api).
var api = app.MapGroup("/api");
api.MapTarefasEndpoints();
api.MapDiagnosticoEndpoints();

// Controllers de API com rota por atributo (DiagnosticoController).
app.MapControllers();

// Rota convencional da interface MVC: /{controller}/{action}/{id?}
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
