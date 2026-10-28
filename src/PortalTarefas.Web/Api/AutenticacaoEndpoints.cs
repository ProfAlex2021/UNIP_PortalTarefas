using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using PortalTarefas.Web.Seguranca;

namespace PortalTarefas.Web.Api;

[Description("Credenciais de acesso.")]
public sealed record Credenciais(
    [property: Required(ErrorMessage = "Informe o e-mail.")]
    [property: EmailAddress(ErrorMessage = "E-mail inválido.")]
    string Email,

    [property: Required(ErrorMessage = "Informe a senha.")]
    string Senha);

[Description("Estado da sessão atual. As permissões são calculadas pelo servidor a partir das políticas.")]
public sealed record Sessao(
    bool Autenticado,
    string? Nome,
    string? Email,
    string? Equipe,
    IReadOnlyList<string> Papeis,
    [property: Description("Políticas gerais atendidas pelo usuário (ex.: tarefas.criar, tarefas.excluir).")]
    IReadOnlyList<string> Permissoes)
{
    public static readonly Sessao Anonima = new(false, null, null, null, [], []);
}

/// <summary>
/// Login, logout e sessão para a SPA. O cookie HttpOnly é emitido pelo Identity;
/// nenhum token ou senha é devolvido ao JavaScript nem registrado em log.
/// </summary>
public static class AutenticacaoEndpoints
{
    public static IEndpointRouteBuilder MapAutenticacao(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/auth").WithTags("Autenticação");

        grupo.MapPost("/login", Entrar)
            .WithName("Entrar")
            .WithSummary("Autentica com e-mail e senha e emite o cookie de sessão")
            .AllowAnonymous()
            .RequireRateLimiting(Capacidade.ConfiguracaoCapacidade.PoliticaLogin) // Roteiro 7: limita força bruta
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/logout", Sair)
            .WithName("Sair")
            .WithSummary("Encerra a sessão (invalida o cookie)")
            .AllowAnonymous();

        grupo.MapGet("/sessao", ObterSessao)
            .WithName("ObterSessao")
            .WithSummary("Informa se há sessão ativa e as permissões do usuário")
            .AllowAnonymous();

        return rotas;
    }

    private static async Task<Results<Ok<Sessao>, ProblemHttpResult>> Entrar(
        Credenciais credenciais,
        UserManager<Usuario> usuarios,
        SignInManager<Usuario> acesso,
        IAuthorizationService autorizacao,
        ILoggerFactory logs,
        HttpContext contexto)
    {
        var auditoria = logs.CreateLogger("Auditoria");
        var usuario = await usuarios.FindByEmailAsync(credenciais.Email);

        // lockoutOnFailure: true -> após 5 falhas a conta fica bloqueada por 5 minutos.
        var resultado = usuario is null
            ? SignInResult.Failed
            : await acesso.PasswordSignInAsync(usuario, credenciais.Senha, isPersistent: false, lockoutOnFailure: true);

        if (!resultado.Succeeded)
        {
            // Mesma resposta para usuário inexistente, senha errada ou conta bloqueada:
            // não revela se o e-mail está cadastrado. O log também não guarda o e-mail digitado.
            auditoria.LogWarning("Falha de login (bloqueado: {Bloqueado})", resultado.IsLockedOut);
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "E-mail ou senha inválidos.");
        }

        auditoria.LogInformation("Login do usuário {UsuarioId}", usuario!.Id);

        // O cookie foi emitido nesta resposta; montamos a sessão com o principal recém-criado.
        var principal = await acesso.CreateUserPrincipalAsync(usuario);
        return TypedResults.Ok(await MontarSessaoAsync(principal, autorizacao));
    }

    private static async Task<NoContent> Sair(
        SignInManager<Usuario> acesso, UserManager<Usuario> usuarios, ILoggerFactory logs, ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated == true && await usuarios.GetUserAsync(usuario) is { } atual)
        {
            // Invalida TODOS os cookies emitidos antes deste momento (inclusive cópias do cookie atual).
            await usuarios.UpdateSecurityStampAsync(atual);
            logs.CreateLogger("Auditoria").LogInformation("Logout do usuário {UsuarioId}", atual.Id);
        }

        await acesso.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Ok<Sessao>> ObterSessao(ClaimsPrincipal usuario, IAuthorizationService autorizacao) =>
        TypedResults.Ok(usuario.Identity?.IsAuthenticated == true
            ? await MontarSessaoAsync(usuario, autorizacao)
            : Sessao.Anonima);

    private static async Task<Sessao> MontarSessaoAsync(ClaimsPrincipal usuario, IAuthorizationService autorizacao)
    {
        var permissoes = new List<string>();
        foreach (var politica in Politicas.Gerais)
            if ((await autorizacao.AuthorizeAsync(usuario, politica)).Succeeded)
                permissoes.Add(politica);

        return new Sessao(
            Autenticado: true,
            Nome: usuario.FindFirstValue("nome") ?? usuario.Identity?.Name,
            Email: usuario.FindFirstValue(ClaimTypes.Email) ?? usuario.Identity?.Name,
            Equipe: usuario.ObterEquipe(),
            Papeis: usuario.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            Permissoes: permissoes);
    }
}
