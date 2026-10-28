using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using PortalTarefas.Web.Dados;

namespace PortalTarefas.Web.Seguranca;

/// <summary>
/// Configuração de autenticação e autorização (Roteiro 6).
/// Esquema escolhido: COOKIE do Identity, HttpOnly + Secure + SameSite=Strict, porque o
/// navegador acessa a API pela mesma origem (proxy do Vite em desenvolvimento e API
/// servindo a SPA em produção). Detalhes da decisão em docs/roteiros/roteiro-06.md.
/// </summary>
public static class ConfiguracaoSeguranca
{
    public const string NomeCookie = "PortalTarefas.Sessao";

    public static IServiceCollection AddSegurancaPortal(this IServiceCollection servicos)
    {
        servicos
            .AddIdentity<Usuario, IdentityRole>(opcoes =>
            {
                // Requisitos de senha adequados ao ambiente didático, sem desligar controles:
                opcoes.Password.RequiredLength = 10;
                opcoes.Password.RequireDigit = true;
                opcoes.Password.RequireLowercase = true;
                opcoes.Password.RequireUppercase = true;
                opcoes.Password.RequireNonAlphanumeric = false; // compensado pelo tamanho mínimo de 10

                // Bloqueio após tentativas falhas (proteção contra força bruta):
                opcoes.Lockout.MaxFailedAccessAttempts = 5;
                opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                opcoes.Lockout.AllowedForNewUsers = true;

                opcoes.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<PortalTarefasDbContext>()
            .AddClaimsPrincipalFactory<FabricaPrincipalUsuario>()
            .AddDefaultTokenProviders();

        servicos.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = NomeCookie;
            cookie.Cookie.HttpOnly = true;                          // inacessível a JavaScript
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always; // só trafega em HTTPS
            cookie.Cookie.SameSite = SameSiteMode.Strict;           // não é enviado por outros sites
            cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
            cookie.SlidingExpiration = true;
            cookie.LoginPath = "/Conta/Login";
            cookie.AccessDeniedPath = "/Conta/AcessoNegado";

            // Para a API: 401/403 em vez de redirecionar para a página de login (que é HTML).
            cookie.Events.OnRedirectToLogin = contexto => ResponderOuRedirecionar(contexto, StatusCodes.Status401Unauthorized);
            cookie.Events.OnRedirectToAccessDenied = contexto => ResponderOuRedirecionar(contexto, StatusCodes.Status403Forbidden);
        });

        // Logout de verdade: no logout o "security stamp" do usuário é trocado e, com intervalo zero,
        // todo cookie é conferido contra o stamp atual. Um cookie copiado antes do logout passa a ser
        // rejeitado (401). Custo: uma consulta ao banco por requisição autenticada.
        servicos.Configure<SecurityStampValidatorOptions>(opcoes => opcoes.ValidationInterval = TimeSpan.Zero);

        servicos.AddAuthorizationBuilder()
            .AddPolicy(Politicas.LerTarefas, p => p.RequireAuthenticatedUser())
            .AddPolicy(Politicas.CriarTarefas, p => p.RequireRole(Papeis.Usuario, Papeis.Administrador))
            .AddPolicy(Politicas.EditarOutrasEquipes, p => p.RequireAssertion(c =>
                c.User.IsInRole(Papeis.Administrador) ||
                c.User.HasClaim(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes)))
            .AddPolicy(Politicas.EditarTarefa, p => p
                .RequireRole(Papeis.Usuario, Papeis.Administrador)
                .AddRequirements(new EdicaoTarefaRequirement()))
            .AddPolicy(Politicas.ExcluirTarefas, p => p.RequireRole(Papeis.Administrador));

        servicos.AddSingleton<IAuthorizationHandler, EdicaoTarefaHandler>();
        return servicos;
    }

    /// <summary>Acrescenta o nome de exibição às claims do cookie (as demais vêm de roles e claims do Identity).</summary>
    public sealed class FabricaPrincipalUsuario(
        UserManager<Usuario> usuarios, RoleManager<IdentityRole> papeis, Microsoft.Extensions.Options.IOptions<IdentityOptions> opcoes)
        : UserClaimsPrincipalFactory<Usuario, IdentityRole>(usuarios, papeis, opcoes)
    {
        protected override async Task<System.Security.Claims.ClaimsIdentity> GenerateClaimsAsync(Usuario usuario)
        {
            var identidade = await base.GenerateClaimsAsync(usuario);
            identidade.AddClaim(new System.Security.Claims.Claim("nome", usuario.NomeExibicao));
            return identidade;
        }
    }

    private static Task ResponderOuRedirecionar(
        Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> contexto,
        int status)
    {
        if (contexto.Request.Path.StartsWithSegments("/api"))
        {
            contexto.Response.StatusCode = status;
            return Task.CompletedTask;
        }
        contexto.Response.Redirect(contexto.RedirectUri);
        return Task.CompletedTask;
    }
}
