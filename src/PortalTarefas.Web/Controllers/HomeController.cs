using Microsoft.AspNetCore.Mvc;

namespace PortalTarefas.Web.Controllers;

/// <summary>Página inicial da interface MVC.</summary>
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
