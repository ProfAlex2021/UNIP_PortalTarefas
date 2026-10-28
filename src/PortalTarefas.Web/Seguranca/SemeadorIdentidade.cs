using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace PortalTarefas.Web.Seguranca;

/// <summary>Usuário de teste descrito na configuração (seção SeedUsuarios). A senha NÃO fica no repositório.</summary>
public sealed class UsuarioSeed
{
    public string Email { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Papel { get; set; } = Papeis.Usuario;
    public string Equipe { get; set; } = string.Empty;
    public bool EditarOutrasEquipes { get; set; }
    /// <summary>Vem de User Secrets ou variável de ambiente (ex.: SeedUsuarios__0__Senha).</summary>
    public string? Senha { get; set; }
}

/// <summary>
/// Seed controlado e idempotente de papéis e usuários de teste.
/// Usuários sem senha configurada são ignorados (com aviso), em vez de receberem uma senha fixa.
/// </summary>
public static class SemeadorIdentidade
{
    public static async Task ExecutarAsync(IServiceProvider servicos, IConfiguration configuracao, ILogger logger)
    {
        var papeis = servicos.GetRequiredService<RoleManager<IdentityRole>>();
        var usuarios = servicos.GetRequiredService<UserManager<Usuario>>();

        foreach (var papel in new[] { Papeis.Usuario, Papeis.Administrador })
            if (!await papeis.RoleExistsAsync(papel))
                await papeis.CreateAsync(new IdentityRole(papel));

        var definicoes = configuracao.GetSection("SeedUsuarios").Get<List<UsuarioSeed>>() ?? [];
        foreach (var definicao in definicoes)
        {
            if (await usuarios.FindByEmailAsync(definicao.Email) is not null)
                continue; // idempotente

            if (string.IsNullOrWhiteSpace(definicao.Senha))
            {
                logger.LogWarning("Usuário de teste {Email} não criado: senha não configurada em User Secrets", definicao.Email);
                continue;
            }

            var usuario = new Usuario
            {
                UserName = definicao.Email,
                Email = definicao.Email,
                EmailConfirmed = true,
                NomeExibicao = definicao.Nome
            };

            var criacao = await usuarios.CreateAsync(usuario, definicao.Senha);
            if (!criacao.Succeeded)
            {
                // Não registra a senha; apenas os códigos de erro do Identity.
                logger.LogError("Falha ao criar usuário de teste {Email}: {Erros}",
                    definicao.Email, string.Join(", ", criacao.Errors.Select(e => e.Code)));
                continue;
            }

            await usuarios.AddToRoleAsync(usuario, definicao.Papel);
            var claims = new List<Claim> { new(TiposClaim.Equipe, definicao.Equipe) };
            if (definicao.EditarOutrasEquipes)
                claims.Add(new Claim(TiposClaim.Permissao, TiposClaim.EditarOutrasEquipes));
            await usuarios.AddClaimsAsync(usuario, claims);

            logger.LogInformation("Usuário de teste {Email} criado com papel {Papel}", definicao.Email, definicao.Papel);
        }
    }
}
