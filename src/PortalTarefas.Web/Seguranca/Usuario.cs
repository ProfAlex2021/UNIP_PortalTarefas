using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace PortalTarefas.Web.Seguranca;

/// <summary>Usuário do ASP.NET Core Identity (senha guardada apenas como hash).</summary>
public class Usuario : IdentityUser
{
    public string NomeExibicao { get; set; } = string.Empty;
}

/// <summary>Nomes de papéis (roles).</summary>
public static class Papeis
{
    public const string Usuario = "Usuario";
    public const string Administrador = "Administrador";
}

/// <summary>Tipos e valores de claims usados nas políticas.</summary>
public static class TiposClaim
{
    public const string Equipe = "equipe";
    public const string Permissao = "permissao";
    public const string EditarOutrasEquipes = "tarefas.editar-outras-equipes";
}

/// <summary>Nomes das políticas de autorização.</summary>
public static class Politicas
{
    public const string LerTarefas = "tarefas.ler";
    public const string CriarTarefas = "tarefas.criar";
    public const string EditarTarefa = "tarefas.editar";              // por recurso (equipe / proprietário)
    public const string EditarOutrasEquipes = "tarefas.editar-outras-equipes";
    public const string ExcluirTarefas = "tarefas.excluir";

    /// <summary>Políticas que não dependem de recurso — avaliadas para montar as permissões da sessão.</summary>
    public static readonly string[] Gerais = [LerTarefas, CriarTarefas, EditarOutrasEquipes, ExcluirTarefas];
}

public static class ClaimsPrincipalExtensions
{
    public static string? ObterId(this ClaimsPrincipal usuario) => usuario.FindFirstValue(ClaimTypes.NameIdentifier);
    public static string? ObterEquipe(this ClaimsPrincipal usuario) => usuario.FindFirstValue(TiposClaim.Equipe);
}
