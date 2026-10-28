using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PortalTarefas.Web.Configuracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using PortalTarefas.Web.Api;
using PortalTarefas.Web.Api.V1;
using PortalTarefas.Web.Dados;
using Scalar.AspNetCore;
using PortalTarefas.Web.Endpoints;
using PortalTarefas.Web.Filtros;
using PortalTarefas.Web.Infraestrutura;
using PortalTarefas.Web.Middlewares;
using PortalTarefas.Web.Capacidade;
using PortalTarefas.Web.Observabilidade;
using PortalTarefas.Web.Resiliencia;
using PortalTarefas.Web.Saude;
using PortalTarefas.Web.Seguranca;
using PortalTarefas.Web.Servicos;
using PortalTarefas.Web.Servicos.Painel;
using PortalTarefas.Web.Validacao;

var builder = WebApplication.CreateBuilder(args);

// Configuração por ambiente (Roteiro 8). Precedência, da menor para a maior:
// appsettings.json < appsettings.{Ambiente}.json < User Secrets (Development) < variáveis de ambiente < argumentos.
// As opções obrigatórias são validadas na inicialização; a lista de variáveis está em .env.example.
builder.Services.AddConfiguracaoValidada();

// Logs, métricas e traces com OpenTelemetry (Roteiro 7).
builder.AddObservabilidade();

// ---------------------------------------------------------------------------
// 1. Registro de serviços (contêiner de injeção de dependência)
// ---------------------------------------------------------------------------

// Problem Details (RFC 9457) como formato padrão de erro + tratador global.
// Toda resposta de erro recebe o identificador de correlação (extensão útil e não sensível).
builder.Services.AddProblemDetails(opcoes =>
    opcoes.CustomizeProblemDetails = contexto =>
    {
        contexto.ProblemDetails.Extensions.TryAdd("correlationId", contexto.HttpContext.TraceIdentifier);
        if (contexto.ProblemDetails is HttpValidationProblemDetails validacao)
            ProblemDetailsPadronizados.AjustarValidacao(validacao);
    });
builder.Services.AddExceptionHandler<TratadorExcecoesGlobal>();

// JSON da API: enums como texto ("Pendente" em vez de 0), mais legível e estável para clientes.
// NumberHandling.Strict: números só como números — o OpenAPI passa a descrevê-los como "integer"
// (o padrão da Web aceita "42" como texto e gera o tipo integer|string no documento).
builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opcoes.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// Documento OpenAPI (Roteiro 4): /openapi/v1.json, também gerado no build em contratos/.
builder.Services.AddDocumentacaoOpenApi();

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
// Sem a connection string, IOptions lança OptionsValidationException com a mensagem de OpcoesConexao.
builder.Services.AddDbContextPool<PortalTarefasDbContext>((servicos, opcoes) =>
    opcoes.UseSqlServer(servicos.GetRequiredService<IOptions<OpcoesConexao>>().Value.PortalTarefas));

// Atrás de um proxy reverso/balanceador que termina o TLS (contêiner em produção),
// confia nos cabeçalhos X-Forwarded-* para saber o esquema (https) e o IP original.
if (builder.Configuration.GetValue<bool>("Proxy:ConfiarCabecalhosEncaminhados"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
    {
        opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        opcoes.KnownIPNetworks.Clear(); // a rede do proxy é definida pela infraestrutura
        opcoes.KnownProxies.Clear();
    });
}

// Serviços da aplicação. O relógio é injetado para facilitar testes de datas.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITarefaService, TarefaServiceEf>();
builder.Services.AddScoped<ValidadorTituloUnico>();
builder.Services.AddScoped<IConsultaPainel, ConsultaPainelEf>();
builder.Services.AddScoped<IConsultaPainel, ConsultaPainelDapper>();
builder.Services.AddScoped<TarefasApiServico>();

// Autenticação e autorização (Roteiro 6): Identity + cookie, papéis, claims e políticas.
builder.Services.AddSegurancaPortal();

// Roteiro 7: health checks, rate limiting, output caching e dependência HTTP resiliente.
builder.Services.AddVerificacoesDeSaude();
builder.Services.AddControlesDeCapacidade();
builder.Services.AddClienteCalendario();
builder.Services.AddSingleton<EstadoSimulacaoCalendario>();

// Política CORS nomeada: somente a origem do front-end local é aceita.
const string PoliticaCorsFrontend = "FrontendLocal";
var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy(PoliticaCorsFrontend, politica => politica
        .WithOrigins(origensPermitidas)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(CorrelacaoMiddleware.NomeCabecalho, "X-Total-Count", "Link", "Location"));
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
    await SemeadorIdentidade.ExecutarAsync(escopo.ServiceProvider, app.Configuration, app.Logger);
}

// ---------------------------------------------------------------------------
// 2. Pipeline HTTP — a ORDEM dos middlewares define o comportamento.
// ---------------------------------------------------------------------------

// (0) Cabeçalhos do proxy reverso, antes de tudo que depende de esquema ou IP (Roteiro 8).
if (app.Configuration.GetValue<bool>("Proxy:ConfiarCabecalhosEncaminhados"))
    app.UseForwardedHeaders();

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

// (9) Autenticação (quem é?) antes da autorização (pode?). Ambas depois do roteamento,
//     para que a autorização conheça o endpoint e suas políticas, e antes dos endpoints.
app.UseAuthentication();
app.UseAuthorization();

// (10) Proteção CSRF para chamadas à API que usam o cookie de sessão.
app.UseMiddleware<ProtecaoCsrfMiddleware>();

// (11) Rate limiting e output caching depois da autenticação: o cache precisa saber se a
//      requisição é de um usuário autenticado (essas não são guardadas nem servidas do cache).
app.UseRateLimiter();
app.UseOutputCache();

// (12) Endpoints.
// API REST v1 (Roteiro 4) — contrato em docs/contrato-api-v1.md.
app.MapTarefasApiV1();
app.MapAutenticacao();
app.MapCalendarioV1();
app.MapPublico();
app.MapVerificacoesDeSaude();               // /health/live e /health/ready

// Dependência externa simulada (somente quando habilitada — Development).
if (app.Configuration.GetValue<bool>("Simulacao:Habilitada"))
    app.MapSimulacaoCalendario();

// Documento OpenAPI e interface interativa (Scalar) somente em desenvolvimento.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                   // /openapi/v1.json
    app.MapScalarApiReference();        // /scalar
}

// Endpoints de API do Roteiro 1, mantidos em um grupo próprio (/api).
var api = app.MapGroup("/api");
api.MapTarefasEndpoints();
api.MapDiagnosticoEndpoints();

// Controllers de API com rota por atributo (DiagnosticoController).
app.MapControllers();

// Rota convencional da interface MVC: /{controller}/{action}/{id?}
// Versão implantada (Roteiro 8): relaciona o que está rodando ao commit que gerou a imagem.
app.MapGet("/versao", (IConfiguration configuracao, IWebHostEnvironment ambiente) => TypedResults.Ok(new
{
    versao = typeof(Program).Assembly.GetName().Version?.ToString(),
    commit = configuracao["PORTALTAREFAS_COMMIT"] ?? "local",
    ambiente = ambiente.EnvironmentName
})).AllowAnonymous().ExcludeFromDescription();

// SPA React integrada (Roteiro 8): o build do front-end é copiado para wwwroot/app na imagem.
// Rotas do cliente como /app/tarefas/3 devolvem o index.html; o React Router decide o resto.
app.MapFallbackToFile("app/{*caminho:nonfile}", "app/index.html").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
