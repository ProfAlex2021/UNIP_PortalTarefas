using PortalTarefas.Web.Endpoints;
using PortalTarefas.Web.Infraestrutura;
using PortalTarefas.Web.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Registro de serviços (contêiner de injeção de dependência)
// ---------------------------------------------------------------------------

// Problem Details (RFC 9457) como formato padrão de erro + tratador global.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorExcecoesGlobal>();

// Validação automática de Data Annotations nos parâmetros das Minimal APIs (.NET 10).
builder.Services.AddValidation();

// Controllers usados na comparação Minimal API x MVC.
builder.Services.AddControllers();

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

// (4) Identificador de correlação (extensão do roteiro): todo log seguinte o carrega.
app.UseCorrelacao();

// (5) Middleware próprio que registra método, caminho, status e duração.
app.UseRegistroRequisicao();

// (6) Arquivos estáticos de wwwroot (encerra a requisição se o arquivo existir).
app.UseStaticFiles();

// (7) Roteamento: escolhe o endpoint que atende a requisição.
app.UseRouting();

// (8) CORS depois do roteamento e antes dos endpoints.
app.UseCors(PoliticaCorsFrontend);

// (9) Endpoints: Minimal APIs e controllers MVC.
app.MapTarefasEndpoints();
app.MapDiagnosticoEndpoints();
app.MapControllers();

app.Run();
