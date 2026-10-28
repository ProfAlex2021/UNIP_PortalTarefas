using Microsoft.AspNetCore.Authorization;
using PortalTarefas.Web.Dominio;

namespace PortalTarefas.Web.Seguranca;

/// <summary>Requisito de autorização avaliado sobre uma tarefa específica (autorização por recurso).</summary>
public sealed class EdicaoTarefaRequirement : IAuthorizationRequirement;

/// <summary>
/// Pode editar a tarefa quem:
///  1) é Administrador; ou
///  2) tem a claim permissao = tarefas.editar-outras-equipes; ou
///  3) pertence à mesma equipe da tarefa; ou
///  4) é o proprietário (quem criou a tarefa) — extensão do Roteiro 6.
/// Caso contrário a requisição recebe 403, mesmo que venha de fora da SPA.
/// </summary>
public sealed class EdicaoTarefaHandler : AuthorizationHandler<EdicaoTarefaRequirement, Tarefa>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto, EdicaoTarefaRequirement requisito, Tarefa tarefa)
    {
        var usuario = contexto.User;

        var autorizado =
            usuario.IsInRole(Papeis.Administrador) ||
            usuario.HasClaim(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes) ||
            (usuario.ObterEquipe() is { } equipe && string.Equals(equipe, tarefa.Equipe, StringComparison.OrdinalIgnoreCase)) ||
            (tarefa.CriadaPorId is not null && tarefa.CriadaPorId == usuario.ObterId());

        if (autorizado)
            contexto.Succeed(requisito);

        return Task.CompletedTask;
    }
}
