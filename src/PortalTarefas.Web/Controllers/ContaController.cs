using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Seguranca;

namespace PortalTarefas.Web.Controllers;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = string.Empty;

    public string? RetornoUrl { get; set; }
}

/// <summary>Login e logout da interface MVC (o mesmo cookie do Identity usado pela SPA).</summary>
[AllowAnonymous]
public sealed class ContaController(
    SignInManager<Usuario> acesso,
    UserManager<Usuario> usuarios,
    ILoggerFactory logs) : Controller
{
    private readonly ILogger _auditoria = logs.CreateLogger("Auditoria");

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { RetornoUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var usuario = await usuarios.FindByEmailAsync(modelo.Email);
        var resultado = usuario is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await acesso.PasswordSignInAsync(usuario, modelo.Senha, isPersistent: false, lockoutOnFailure: true);

        if (!resultado.Succeeded)
        {
            _auditoria.LogWarning("Falha de login (bloqueado: {Bloqueado})", resultado.IsLockedOut);
            ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos."); // não revela se o e-mail existe
            modelo.Senha = string.Empty;
            return View(modelo);
        }

        _auditoria.LogInformation("Login do usuário {UsuarioId}", usuario!.Id);
        // Só redireciona para URLs locais (evita "open redirect").
        return Url.IsLocalUrl(modelo.RetornoUrl) ? LocalRedirect(modelo.RetornoUrl!) : RedirectToAction("Index", "Tarefas");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true && await usuarios.GetUserAsync(User) is { } atual)
        {
            await usuarios.UpdateSecurityStampAsync(atual); // invalida cookies emitidos antes do logout
            _auditoria.LogInformation("Logout do usuário {UsuarioId}", atual.Id);
        }
        await acesso.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AcessoNegado()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
